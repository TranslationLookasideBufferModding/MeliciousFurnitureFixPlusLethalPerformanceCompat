using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MliciosFurnitureFixPlus
{
    internal static class Patches
    {
        internal static void Install(Harmony harmony)
        {
            Type originalPlugin = AccessTools.TypeByName("MelanieMeliciousFurniture.Plugin");
            Type furniture = originalPlugin == null ? null : originalPlugin.Assembly.GetType("FireExitPatch");
            Type loader = AccessTools.TypeByName("LethalLevelLoader.DungeonLoader");
            MethodInfo beforeLoad = Required(furniture, "PreLevelLoad");
            MethodInfo beforeLeft = Required(furniture, "PreShipLeft");
            MethodInfo getEntrances = Required(loader, "GetEntranceTeleports");
            MethodInfo patchExits = Required(loader, "PatchFireEscapes");
            MethodInfo setIds = Required(typeof(RoundManager), "SetExitIDs");
            MethodInfo find = Required(typeof(EntranceTeleport), "FindExitPoint");
            MethodInfo shipLeft = Required(typeof(StartOfRound), "ShipHasLeft");
            // Patch original helpers rather than removing another mod's Harmony ownership.
            harmony.Patch(beforeLoad, prefix: Hook("SkipOriginal"));
            harmony.Patch(beforeLeft, prefix: Hook("SkipOriginal"));
            harmony.Patch(patchExits, prefix: Hook("Prepare"), postfix: Hook("CaptureIds"));
            harmony.Patch(getEntrances, postfix: Hook("Register"));
            harmony.Patch(setIds, transpiler: Hook("FreshExitScan"), postfix: Hook("Resolve"));
            harmony.Patch(find, prefix: Hook("Find"));
            harmony.Patch(Required(typeof(EntranceTeleport), "Awake"), postfix: Hook("Created"));
            harmony.Patch(shipLeft, prefix: Hook("Reset"));
            Type dawn = AccessTools.TypeByName("Dawn.DungeonRegistrationHandler");
            if (dawn != null)
                harmony.Patch(Required(dawn, "AdjustFireExits"), prefix: Hook("PrepareDawn"), transpiler: Hook("FreshDawnScan"), postfix: Hook("CaptureDawnIds"));
            // Their wrappers may already have been JIT-compiled before this addon loads.
            // Remove exactly these two prefixes as well, so an inlined helper cannot reparent furniture.
            harmony.Unpatch(Required(typeof(RoundManager), "LoadNewLevel"), beforeLoad);
            harmony.Unpatch(shipLeft, beforeLeft);
        }
        private static MethodInfo Required(Type type, string name)
        {
            MethodInfo method = type == null ? null : AccessTools.Method(type, name);
            if (method == null) throw new MissingMethodException(type == null ? "missing dependency type" : type.FullName, name);
            return method;
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(Patches), name) { priority = Priority.First }; }
        private static bool SkipOriginal() { return false; }
        private static void Prepare() { Runtime.Prepare(); }
        private static void PrepareDawn() { Runtime.Prepare(); }
        private static void CaptureIds() { Runtime.CaptureIds(); }
        private static void CaptureDawnIds()
        {
            if (Runtime.HasRegisteredShip) Runtime.Log("DawnLib fire-exit generation handled on this peer.");
            Runtime.CaptureIds();
        }
        private static void Register(List<EntranceTeleport> __result) { Runtime.Register(__result); }
        private static void Resolve() { Runtime.Resolve(); }
        private static void Reset() { Runtime.Reset(); }
        private static void Created(EntranceTeleport __instance) { Runtime.OnCreated(__instance); }
        private static bool Find(EntranceTeleport __instance, ref bool __result)
        {
            bool found;
            if (!Runtime.TryFind(__instance, out found)) return true;
            __result = found;
            return false;
        }
        // Only SetExitIDs uses this scan. All other LP caches and rendering optimizations remain installed.
        private static EntranceTeleport[] ScanForExitIds()
        {
            return Runtime.HasRegisteredShip ? Runtime.FreshScan() : UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
        }
        private static EntranceTeleport[] ScanForDawnIds(UnityEngine.FindObjectsInactive inactive, UnityEngine.FindObjectsSortMode sortMode)
        {
            // This one generation-time scan must also exclude stored ship doors and old interior exits.
            return Runtime.RegisterDawn();
        }
        private static IEnumerable<CodeInstruction> FreshDawnScan(IEnumerable<CodeInstruction> instructions)
        {
            int replacements = 0;
            MethodInfo replacement = AccessTools.Method(typeof(Patches), "ScanForDawnIds");
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && called != null && called.DeclaringType == typeof(UnityEngine.Object) && called.Name == "FindObjectsByType" && called.IsGenericMethod && called.GetGenericArguments()[0] == typeof(EntranceTeleport) && called.GetParameters().Length == 2)
                {
                    instruction.operand = replacement;
                    replacements++;
                }
                yield return instruction;
            }
            if (replacements != 1) throw new NotSupportedException("Unexpected DawnLib AdjustFireExits layout: expected one entrance scan, found " + replacements + ".");
        }
        private static IEnumerable<CodeInstruction> FreshExitScan(IEnumerable<CodeInstruction> instructions)
        {
            int replacements = 0;
            MethodInfo replacement = AccessTools.Method(typeof(Patches), "ScanForExitIds");
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && called != null && called.DeclaringType == typeof(UnityEngine.Object) && called.Name == "FindObjectsOfType" && called.IsGenericMethod && called.GetGenericArguments()[0] == typeof(EntranceTeleport) && called.GetParameters().Length == 0)
                {
                    instruction.operand = replacement;
                    replacements++;
                }
                yield return instruction;
            }
            if (replacements != 1) throw new NotSupportedException("Unexpected v81 SetExitIDs method: expected exactly one entrance scan, found " + replacements + ".");
        }
    }
}
