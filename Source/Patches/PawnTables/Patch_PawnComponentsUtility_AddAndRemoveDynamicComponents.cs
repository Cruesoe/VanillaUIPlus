using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Applies when a pawn's timetable or policy trackers are first created for the player, including pawns generated straight into the faction; loading a save is unaffected.
[HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents))]
public static class Patch_PawnComponentsUtility_AddAndRemoveDynamicComponents
{
    public static void Prefix(Pawn pawn, ref (bool noOutfits, bool noTimetable) __state)
    {
        __state = (pawn.outfits == null, pawn.timetable == null);
    }

    public static void Postfix(Pawn pawn, (bool noOutfits, bool noTimetable) __state)
    {
        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            return;
        }

        if (__state.noTimetable && pawn.timetable != null)
        {
            DefaultSchedule.ApplyTo(pawn);
        }

        if (__state.noOutfits && pawn.outfits != null)
        {
            DefaultAssignments.ApplyTo(pawn);
        }
    }
}
