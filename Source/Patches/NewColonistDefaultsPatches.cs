using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Pawns switching into the player faction (recruits, slaves, refugees); a prisoner here is being enslaved and keeps the slave medical default.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
public static class Patch_Pawn_SetFaction_NewColonistDefaults
{
    public static void Prefix(Pawn __instance, ref NewColonistDefaults.JoinState __state)
    {
        __state.wasPlayer = NewColonistDefaults.IsPlayerPawn(__instance);
        __state.wasPrisoner = __instance.IsPrisoner;
    }

    public static void Postfix(Pawn __instance, NewColonistDefaults.JoinState __state)
    {
        if (__state.wasPlayer || !NewColonistDefaults.IsPlayerPawn(__instance))
        {
            return;
        }

        DefaultSchedule.ApplyTo(__instance);
        DefaultAssignments.ApplyTo(__instance, includeMedicalCare: !__state.wasPrisoner);
    }
}

// Starting colonists, reapplied after map-gen prep, which resets their work priorities.
[HarmonyPatch(typeof(GameInitData), nameof(GameInitData.PrepForMapGen))]
public static class Patch_GameInitData_PrepForMapGen_NewColonistDefaults
{
    public static void Postfix(GameInitData __instance)
    {
        foreach (Pawn pawn in __instance.startingAndOptionalPawns)
        {
            DefaultSchedule.ApplyTo(pawn);
            DefaultWorkPriorities.ApplyTo(pawn);
            DefaultAssignments.ApplyTo(pawn);
        }
    }
}

// Runs once, when a pawn first becomes able to work for the colony.
[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
public static class Patch_Pawn_WorkSettings_EnableAndInitialize
{
    public static void Postfix(Pawn ___pawn)
    {
        DefaultWorkPriorities.ApplyTo(___pawn);
    }
}

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
