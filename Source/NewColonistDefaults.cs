using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Join hooks shared by the pinned defaults (schedule, work priorities, assignments); hooks only one default needs stay with it.
/// </summary>
public static class NewColonistDefaults
{
    // Uses Faction.IsPlayer rather than Faction.OfPlayer, which isn't safe while a save loads.
    public static bool IsPlayerPawn(Pawn pawn)
    {
        return pawn.Faction != null && pawn.Faction.IsPlayer;
    }

    // Colonists and slaves; not colony mechs or mutants, whose work and policies the game handles itself.
    public static bool IsColonyHumanlike(Pawn pawn)
    {
        return pawn.RaceProps.Humanlike && !pawn.IsMutant && IsPlayerPawn(pawn);
    }

    public struct JoinState
    {
        public bool wasPlayer;
        public bool wasPrisoner;
    }
}

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
