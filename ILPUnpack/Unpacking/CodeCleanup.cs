using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ILPUnpack.Unpacking {
	internal sealed class CodeCleaner {
		internal HashSet<TypeDef> DelegateTypes { get; } = new HashSet<TypeDef>(TypeEqualityComparer.Instance);
		internal ModuleDef Module { private get; set; }

		internal void CleanGlobalType(FieldDef invokeField, FieldDef stringField) {
			var globalType = Module.GlobalType;
			globalType.Fields.Remove(invokeField);
			globalType.Fields.Remove(stringField);

			var cctor = globalType.FindStaticConstructor();
			if (cctor?.HasBody == true) {
				var iunknownCall = cctor.Body.Instructions.FirstOrDefault(inst =>
					inst.OpCode.Code == Code.Call && inst.Operand is MemberRef memberRef && memberRef.FullName ==
					"System.IntPtr System.Runtime.InteropServices.Marshal::GetIUnknownForObject(System.Object)");
				if (iunknownCall is null)
					return;
				var releaseCall = cctor.Body.Instructions.FirstOrDefault(inst =>
					inst.OpCode.Code == Code.Call && inst.Operand is MemberRef memberRef && memberRef.FullName ==
					"System.Int32 System.Runtime.InteropServices.Marshal::Release(System.IntPtr)");
				if (releaseCall is null)
					return;

				int startIndex = cctor.Body.Instructions.IndexOf(iunknownCall) - 2;
				int endIndex = cctor.Body.Instructions.IndexOf(releaseCall) + 2;
				if (startIndex < 0 || endIndex < 0 || startIndex >= endIndex)
					return;

				var exHandler = cctor.Body.ExceptionHandlers.FirstOrDefault(exh => exh.HandlerEnd == cctor.Body.Instructions[endIndex + 1]);
				if (exHandler is null)
					return;

				var platformMethods = new Core.Tuple<MethodDef, MethodDef>();
				for (int i = cctor.Body.Instructions.IndexOf(exHandler.TryStart); i < cctor.Body.Instructions.IndexOf(exHandler.TryEnd); i++) {
					var instr = cctor.Body.Instructions[i];
					if (instr.OpCode.Code == Code.Call &&
					    instr.Operand is MethodDef calledMethod && IsPlatformDependantMethod(calledMethod)) {
						if (platformMethods.Item1 is null)
							platformMethods.Item1 = calledMethod;
						else {
							platformMethods.Item2 = calledMethod;
							break;
						}
					}
				}
				if (platformMethods.Item1 is null || platformMethods.Item2 is null)
					return;

				if (platformMethods.Item1.IsPinvokeImpl && platformMethods.Item2.IsPinvokeImpl) {
					globalType.Methods.Remove(platformMethods.Item1);
					globalType.Methods.Remove(platformMethods.Item2);
				}
				else
					KillEmbeddedDllCalls(globalType, platformMethods.Item1, platformMethods.Item2);

				cctor.Body.ExceptionHandlers.Remove(exHandler);

				for (int i = startIndex; i <= endIndex; i++)
					cctor.Body.Instructions.Remove(cctor.Body.Instructions[startIndex]);
			}
		}

		internal void CleanDelegateTypes() {
			foreach (var dType in DelegateTypes.Where(dType => dType is not null)) {
				if (dType.IsNested)
					dType.DeclaringType.NestedTypes.Remove(dType);
				else
					Module.Types.Remove(dType);
			}
		}

		private static bool IsPlatformDependantMethod(MethodDef method) {
			return method.DeclaringType.IsGlobalModuleType && method.ReturnType.ElementType == ElementType.Boolean &&
				   method.Parameters.Count == 2 &&
				   method.Parameters[0].Type.ElementType == ElementType.I4 &&
				   method.Parameters[1].Type.ElementType == ElementType.I;
		}

		private void KillEmbeddedDllCalls(TypeDef globalType, MethodDef dll32, MethodDef dll64) {
			// TODO: Don't rely on static offsets into the method body!
			var ptrMethodInstr = dll32.Body.Instructions.FirstOrDefault(x =>
				x.OpCode.Code == Code.Call && x.Operand is MethodDef methodDef && IsGetP0DelegateMethod(methodDef));
			if (ptrMethodInstr is null)
				return;

			globalType.Methods.Remove(dll32);
			globalType.Methods.Remove(dll64);

			var delegateType = (TypeDef)dll32.Body.Instructions[1].Operand;
			Module.Types.Remove(delegateType);

			var ptrMethod = (MethodDef)ptrMethodInstr.Operand;
			globalType.Methods.Remove(ptrMethod);

			var getDllMethod = (MethodDef)ptrMethod.Body.Instructions[0].Operand;
			globalType.Methods.Remove(getDllMethod);
			var getAddress = (MethodDef)ptrMethod.Body.Instructions[2].Operand;
			globalType.Methods.Remove(getAddress);

			// HACK: Do not remove the resources if we are unpacking ILProtector itself.
			// They are used during the protection process.
			if (Module.Name != "ILProtector.exe") {
				string resName64 = (string)getDllMethod.Body.Instructions[3].Operand;
				string resName32 = (string)getDllMethod.Body.Instructions[5].Operand;
				Module.Resources.Remove(Module.Resources.Find(resName64));
				Module.Resources.Remove(Module.Resources.Find(resName32));
			}

			var getModHandleMethod = (MethodDef)getDllMethod.Body.Instructions[8].Operand;
			var writeDllMethod = (MethodDef)getDllMethod.Body.Instructions[26].Operand;
			var loadLibraryMethod = (MethodDef)getDllMethod.Body.Instructions[28].Operand;
			globalType.Methods.Remove(getModHandleMethod);
			globalType.Methods.Remove(writeDllMethod);
			globalType.Methods.Remove(loadLibraryMethod);
		}

		private static bool IsGetP0DelegateMethod(MethodDef method) {
			return method.DeclaringType.IsGlobalModuleType && method.ReturnType.FullName == "System.Delegate" &&
				   method.Parameters.Count == 2 &&
				   method.Parameters[0].Type.ElementType == ElementType.String &&
				   method.Parameters[1].Type.FullName == "System.Type";
		}
	}
}
