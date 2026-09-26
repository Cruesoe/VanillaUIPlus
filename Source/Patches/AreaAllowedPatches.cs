using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

// The table whose allowed-area cell is being drawn, for the area selector patch below.
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

// Shift-clicking any area button applies that area to every eligible pawn in the table.
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
