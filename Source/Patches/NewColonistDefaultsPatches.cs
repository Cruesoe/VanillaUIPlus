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

// Pawns generated straight into the player faction; skipped while loading, when saved times overwrite it anyway.
[HarmonyPatch(typeof(Pawn_TimetableTracker), MethodType.Constructor, new[] { typeof(Pawn) })]
public static class Patch_Pawn_TimetableTracker_Ctor
{
    public static void Postfix(Pawn pawn)
    {
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            DefaultSchedule.ApplyTo(pawn);
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

// Applies when a pawn's policy trackers are first created for the player; loading a save is unaffected.
[HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents))]
public static class Patch_PawnComponentsUtility_AddAndRemoveDynamicComponents
{
    public static void Prefix(Pawn pawn, ref bool __state)
    {
        __state = pawn.outfits == null;
    }

    public static void Postfix(Pawn pawn, bool __state)
    {
        if (__state && pawn.outfits != null && Scribe.mode == LoadSaveMode.Inactive)
        {
            DefaultAssignments.ApplyTo(pawn);
        }
    }
}
