using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ILPUnpack.Unpacking {
	internal sealed class HarmonyHook {
		private const string harmonyID = "me.elektrokill.ilpunpack";

		private static readonly List<DynamicMethod> dumpedMethods = new List<DynamicMethod>();
		private static readonly Type runtimeMethodHandleType = typeof(RuntimeMethodHandle);
		private static MethodBase spoofedMethod;

		private readonly Harmony harmony;

		internal HarmonyHook() {
			harmony = new Harmony(harmonyID);
		}

		internal void ApplyMethodPatch() {
			var corlib = typeof(Type).Assembly;
			var runtimeType = corlib.GetType("System.RuntimeType");

			var getMethod = runtimeType.GetMethod("GetMethodBase", BindingFlags.NonPublic | BindingFlags.Static, null,
				Environment.Version.Major == 2
					? new[] { runtimeMethodHandleType }
					: new[] { runtimeType, corlib.GetType("System.IRuntimeMethodInfo") }, null);

			harmony.Patch(getMethod, null, new HarmonyMethod(typeof(HarmonyHook).GetMethod(nameof(GetMethodBasePostFix), BindingFlags.NonPublic | BindingFlags.Static)));
		}

		private static void GetMethodBasePostFix(ref MethodBase __result) {
			// InvokeMethod for CLR 4, _InvokeMethodFast for CLR 2
			if ((__result.Name == "InvokeMethod" || __result.Name == "_InvokeMethodFast") && __result.DeclaringType == runtimeMethodHandleType)
				__result = spoofedMethod;
		}

		internal void SetSpoofedMethod(MethodBase method) {
			spoofedMethod = method;
		}

		internal void ApplyRuntimeDumpPatch() {
			harmony.Patch(typeof(DynamicMethod).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic),
				new HarmonyMethod(typeof(HarmonyHook).GetMethod(nameof(InitPreFix),
					BindingFlags.NonPublic | BindingFlags.Static)));
		}

		private static void InitPreFix(ref DynamicMethod __instance) {
			dumpedMethods.Add(__instance);
		}

		internal List<DynamicMethod> GetDumpedMethods() {
			return dumpedMethods;
		}

		internal void UndoPatches() {
			harmony.UnpatchAll(harmonyID);
		}
	}
}
