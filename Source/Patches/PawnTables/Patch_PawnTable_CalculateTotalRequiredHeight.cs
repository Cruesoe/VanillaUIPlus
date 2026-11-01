using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(PawnTable), "CalculateTotalRequiredHeight")]
public static class Patch_PawnTable_CalculateTotalRequiredHeight
{
    public static void Postfix(PawnTable __instance, ref float __result)
    {
        if (AnimalGroups.Get(__instance) is { } groups)
        {
            __result += groups.Count * AnimalGroups.HeaderHeight;
        }
    }
}
