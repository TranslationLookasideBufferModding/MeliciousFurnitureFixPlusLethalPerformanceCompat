using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

public static class CacheSimulation
{
    public static int Calls;
    private static bool MissingObjectLookup() { Calls++; return false; }
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CacheSimulation), "MissingObjectLookup"));
        yield return new CodeInstruction(OpCodes.Ret);
    }
}
