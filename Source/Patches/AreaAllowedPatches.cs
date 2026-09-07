using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

// AreaAllowedGUI.DoAreaSelector draws one area button for one pawn and has no idea which
// PawnTable it is part of, so it cannot reach the other rows to mass-assign them. The
// column worker's DoCell does know the table (it is handed one per row), so a Prefix
// there stashes it here just long enough for the selector Prefix below to read it. Both
// run on the UI thread within the same synchronous draw pass, so a plain static field is
// safe - there is no reentrancy to worry about.
public static class AreaAllowedPatches
{
    public static PawnTable? CurrentTable;
}

[HarmonyPatch(typeof(PawnColumnWorker_AllowedArea), nameof(PawnColumnWorker_AllowedArea.DoCell))]
public static class Patch_PawnColumnWorker_AllowedArea_DoCell
{
    public static void Prefix(PawnTable table)
    {
        AreaAllowedPatches.CurrentTable = table;
    }
}

// Vanilla only offers a mass-assign shortcut from the column header (shift-click for Home,
// shift-right-click for Unrestricted). This extends the same idea to every area button in
// every row: shift-clicking any area for one pawn applies that same area to every other
// eligible pawn in the table, not just Home.
[HarmonyPatch(typeof(AreaAllowedGUI), "DoAreaSelector")]
public static class Patch_AreaAllowedGUI_DoAreaSelector
{
    public static void Prefix(Rect rect, Area area)
    {
        if (!UiPlusMod.Settings.shiftClickAssignAreaToAll
            || !Event.current.shift
            || Event.current.type != EventType.MouseDown
            || Event.current.button != 0
            || !Mouse.IsOver(rect.ContractedBy(1f)))
        {
            return;
        }

        PawnTable? table = AreaAllowedPatches.CurrentTable;
        if (table == null)
        {
            return;
        }

        foreach (Pawn pawn in table.PawnsListForReading)
        {
            if (pawn.Faction == Faction.OfPlayer && pawn.playerSettings != null && pawn.playerSettings.SupportsAllowedAreas)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            }
        }

        SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
    }
}
