using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// PawnTable_Animals doesn't override PrimarySortFunction, so the base method is patched and filtered to animal tables.
[HarmonyPatch(typeof(PawnTable), "PrimarySortFunction")]
public static class Patch_PawnTable_PrimarySortFunction
{
    public static void Postfix(PawnTable __instance, ref IEnumerable<Pawn> __result)
    {
        if (!AnimalGroups.Applies(__instance))
        {
            AnimalGroups.Clear(__instance);
            return;
        }

        __result = AnimalGroups.Group(__instance, __result);
    }
}
