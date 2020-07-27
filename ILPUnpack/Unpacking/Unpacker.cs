using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using ILPUnpack.Core;
using ILogger = ILPUnpack.Core.ILogger;

namespace ILPUnpack.Unpacking {
	internal sealed class Unpacker {
		private readonly ILogger logger;
		private readonly IUnpackerParameters parameters;
		private readonly HarmonyHook hook;
		private readonly CodeCleaner codeCleaner;
		private int stringsDecrypted, methodsRestored;

		private Core.Tuple<FieldDef, MethodDef> bodyDelegateInfo, stringDelegateInfo;

		internal bool Success { get; private set; }

		internal Unpacker(ILogger logger, IUnpackerParameters parameters) {
			this.logger = logger;
			this.parameters = parameters;
			hook = new HarmonyHook();
			codeCleaner = new CodeCleaner();
		}

		internal void Run() {
			try {
				bodyDelegateInfo = new Core.Tuple<FieldDef, MethodDef>();
				stringDelegateInfo = new Core.Tuple<FieldDef, MethodDef>();

				var asmResolver = new AssemblyResolver();
				asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);

				logger.Info("Loading module...");

				var module = ModuleDefMD.Load(parameters.FilePath, asmResolver.DefaultModuleContext);
				module.Location = parameters.FilePath;
				module.EnableTypeDefFindCache = true;
				codeCleaner.Module = module;

				if (module.IsClr1x)
					logger.Warn("CLR 1.x is not supported! This may lead to unexpected results!");
				else if (Environment.Version.Major == 2 && !module.IsClr20 || Environment.Version.Major == 4 && !module.IsClr40)
					logger.Warn("CLR Mismatch detected! This may lead to unexpected results!");

				var reflModule = Assembly.LoadFrom(parameters.FilePath).ManifestModule;

				logger.Info("Setting up...");

				logger.Debug("Running global cctor...");
				if (parameters.DumpRuntime)
					hook.ApplyRuntimeDumpPatch();

				RuntimeHelpers.RunModuleConstructor(reflModule.ModuleHandle);

				if (parameters.DumpRuntime) {
					logger.Debug("Dumping runtime...");
					var assembly = AppDomain.CurrentDomain.GetAssemblies().First(x => x.GetName().Name == "Protect");
					var dumpedMod = ModuleDefMD.Load(assembly.ManifestModule, asmResolver.DefaultModuleContext);
					asmResolver.AddToCache(dumpedMod);
					int counter = 0, failed = 0;
					foreach (var dynamicMethod in hook.GetDumpedMethods()) {
						try {
							var reader = new DynamicMethodBodyReader(dumpedMod, dynamicMethod);
							reader.Read();
							var dumpedMethod = reader.GetMethod();
							// Exclude method bodies with invalid operands.
							if (dumpedMethod.Body.Instructions.Any(x =>
								x.OpCode.OperandType != OperandType.InlineNone && x.Operand is null))
								continue;
							dumpedMethod.Name = Guid.NewGuid().ToString();
							dumpedMod.GlobalType.Methods.Add(dumpedMethod);
							counter++;
						}
						catch {
							failed++;
						}
					}

					logger.Debug(failed > 0
						? $"Dumped {counter} runtime dynamic methods. (Failed: {failed})"
						: $"Dumped {counter} runtime dynamic methods.");


					string path = Path.Combine(Path.GetDirectoryName(parameters.FilePath), "ProtectRuntime.dll");
					WriteModule(dumpedMod, path);
					asmResolver.Remove(dumpedMod);
					dumpedMod.Dispose();

					hook.UndoPatches();
				}

				logger.Debug("Resolving fields...");
				bodyDelegateInfo.Item1 = module.GlobalType.FindField("Invoke");
				stringDelegateInfo.Item1 = module.GlobalType.FindField("String");

				var bodyDelegate = bodyDelegateInfo.Item1?.FieldType.ToTypeDefOrRefSig().TypeDef;
				var stringDelegate = stringDelegateInfo.Item1?.FieldType.ToTypeDefOrRefSig().TypeDef;

				codeCleaner.DelegateTypes.Add(bodyDelegate);
				codeCleaner.DelegateTypes.Add(stringDelegate);

				bodyDelegateInfo.Item2 = bodyDelegate?.FindMethod("Invoke");
				int? invokeToken = bodyDelegateInfo.Item2?.MDToken.ToInt32();
				stringDelegateInfo.Item2 = stringDelegate?.FindMethod("Invoke");
				int? stringToken = stringDelegateInfo.Item2?.MDToken.ToInt32();

				if (bodyDelegateInfo.Item1 is null || invokeToken is null)
					throw new NullReferenceException("Cannot find ILP Invoke field");

				var invokeInstance = reflModule.ResolveField(bodyDelegateInfo.Item1.MDToken.ToInt32());
				var bodyInvoke = reflModule.ResolveMethod(invokeToken.Value);
				object bodyFieldInst = invokeInstance.GetValue(null);

				MethodBase stringInvoke = null;
				object stringFieldInst = null;
				if (stringToken is not null && stringDelegateInfo.Item1 is not null) {
					var strInstance = reflModule.ResolveField(stringDelegateInfo.Item1.MDToken.ToInt32());
					stringInvoke = reflModule.ResolveMethod(stringToken.Value);
					stringFieldInst = strInstance.GetValue(null);
				}
				else
					logger.Debug("Couldn't find string protection.");

				logger.Debug("Applying patches...");
				hook.ApplyMethodPatch();

				logger.Info("Processing methods...");
				foreach (var type in module.GetTypes()) {
					foreach (var method in type.Methods) {
						if (!method.HasBody)
							continue;
						hook.SetSpoofedMethod(reflModule.ResolveMethod(method.MDToken.ToInt32()));

						var importer = new Importer(module, ImporterOptions.TryToUseDefs | ImporterOptions.TryToUseExistingAssemblyRefs, GenericParamContext.Create(method));
						RestoreMethod(method, importer, bodyInvoke, bodyFieldInst);

						if (stringInvoke is not null && stringFieldInst is not null)
							DecryptStrings(method, stringInvoke, stringFieldInst);
					}
				}

				logger.Info("Cleaning up...");

				logger.Debug("Undoing patches...");
				hook.UndoPatches();

				if (!parameters.NoCleanup) {
					module.EnableTypeDefFindCache = false;

					logger.Debug("Cleaning global type...");
					codeCleaner.CleanGlobalType(bodyDelegateInfo.Item1, stringDelegateInfo.Item1);

					logger.Debug("Cleaning delegate types...");
					codeCleaner.CleanDelegateTypes();
				}

				logger.Info("Writing module...");

				string newFilePath = Path.Combine(Path.GetDirectoryName(parameters.FilePath), $"{Path.GetFileNameWithoutExtension(parameters.FilePath)}-Unpacked{Path.GetExtension(parameters.FilePath)}");
				WriteModule(module, newFilePath);

				module.Dispose();
				logger.Info($"Successfully restored {methodsRestored} method bodies and decrypted {stringsDecrypted} strings.");
				Success = true;
			}
			catch (Exception ex) {
				logger.Error($"An error occured during unpacking: {ex}");
				Success = false;
			}
		}

		private void RestoreMethod(MethodDef methodDef, Importer importer, MethodBase invokeMethod, object fieldInstance) {
			if (methodDef.Body.Instructions.Count < 9)
				return;
			var firstInstr = methodDef.Body.Instructions[0];
			if (firstInstr.OpCode.Code != Code.Ldsfld || firstInstr.Operand != bodyDelegateInfo.Item1)
				return;
			var thirdInstr = methodDef.Body.Instructions[2];
			if (thirdInstr.OpCode.Code != Code.Callvirt || thirdInstr.Operand != bodyDelegateInfo.Item2)
				return;

			codeCleaner.DelegateTypes.Add((TypeDef)methodDef.Body.Instructions[3].Operand);

			int index = methodDef.Body.Instructions[1].GetLdcI4Value();

			var dynamicMethodBodyReader = new DynamicMethodBodyReader(methodDef.Module,
				invokeMethod.Invoke(fieldInstance, new object[] { index }), importer);
			dynamicMethodBodyReader.Read();

			dynamicMethodBodyReader.RestoreMethod(methodDef);
			methodsRestored++;
		}

		private void DecryptStrings(MethodDef methodDef, MethodBase invokeMethod, object fieldInstance) {
			if (methodDef.Body.Instructions.Count < 3)
				return;
			for (int i = 2; i < methodDef.Body.Instructions.Count; i++) {
				var currentInstr = methodDef.Body.Instructions[i];
				if (currentInstr.OpCode.Code != Code.Callvirt || currentInstr.Operand != stringDelegateInfo.Item2)
					continue;
				var beforeInstruction = methodDef.Body.Instructions[i - 1];
				var beforeBeforeInstr = methodDef.Body.Instructions[i - 2];
				if (beforeBeforeInstr.OpCode.Code != Code.Ldsfld || beforeBeforeInstr.Operand != stringDelegateInfo.Item1)
					continue;
				int index = beforeInstruction.GetLdcI4Value();
				currentInstr.OpCode = OpCodes.Ldstr;
				currentInstr.Operand = (string)invokeMethod.Invoke(fieldInstance, new object[] { index });
				beforeInstruction.OpCode = OpCodes.Nop;
				beforeInstruction.Operand = null;
				beforeBeforeInstr.OpCode = OpCodes.Nop;
				beforeBeforeInstr.Operand = null;
				stringsDecrypted++;
			}
		}

		private void WriteModule(ModuleDefMD module, string filePath) {
			ModuleWriterOptionsBase modOpts;

			if (!module.IsILOnly || module.VTableFixups is not null)
				modOpts = new NativeModuleWriterOptions(module, true);
			else
				modOpts = new ModuleWriterOptions(module);

			module.EnableTypeDefFindCache = true;

			if (parameters.PreserveMetadata) {
				modOpts.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
				modOpts.MetadataOptions.PreserveHeapOrder(module, true);
			}

			if (parameters.KeepExtraPEData && modOpts is NativeModuleWriterOptions nativeOpts) {
				nativeOpts.KeepWin32Resources = true;
				nativeOpts.KeepExtraPEData = true;
			}

			modOpts.Logger = modOpts.MetadataLogger = new DnlibLogger(logger);

			if (modOpts is NativeModuleWriterOptions nativeOptions)
				module.NativeWrite(filePath, nativeOptions);
			else
				module.Write(filePath, (ModuleWriterOptions)modOpts);
		}
	}
}
