using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
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

// Fields are taken by ref so they are read after the recache below.
[HarmonyPatch(typeof(PawnTable), nameof(PawnTable.PawnTableOnGUI))]
public static class Patch_PawnTable_PawnTableOnGUI_AnimalGroups
{
    public static bool Prefix(PawnTable __instance, Vector2 position, ref Vector2 ___scrollPosition, ref Vector2 ___cachedSize,
        ref List<float> ___cachedColumnWidths, ref List<float> ___cachedRowHeights, ref List<Pawn> ___cachedPawns,
        ref List<LookTargets> ___cachedLookTargets, ref float ___cachedHeaderHeight, ref float ___cachedHeightNoScrollbar)
    {
        if (Event.current.type == EventType.Layout)
        {
            return true;
        }

        // Recaches when the setting changed since the table was last built.
        bool applies = AnimalGroups.Applies(__instance);
        if (applies != (AnimalGroups.Get(__instance) != null))
        {
            __instance.SetDirty();
        }

        _ = __instance.Size;
        if (!applies || AnimalGroups.Get(__instance) is not { } groups)
        {
            return true;
        }

        AnimalGroups.Draw(__instance, groups, position, ref ___scrollPosition, ___cachedSize, ___cachedColumnWidths, ___cachedRowHeights,
            ___cachedPawns, ___cachedLookTargets, ___cachedHeaderHeight, ___cachedHeightNoScrollbar);
        return false;
    }
}

// MainTabWindow_Animals doesn't override PostOpen, so the base method is patched and filtered to the Animals tab.
[HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.PostOpen))]
public static class Patch_MainTabWindow_PawnTable_PostOpen
{
    public static void Prefix(MainTabWindow_PawnTable __instance)
    {
        if (__instance is MainTabWindow_Animals)
        {
            AnimalGroups.CollapseAll();
        }
    }
}
