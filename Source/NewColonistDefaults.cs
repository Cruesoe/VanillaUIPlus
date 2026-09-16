using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Shared hooks for the pinned defaults (schedule, work priorities, assignments) where a pawn
/// joins the colony, so each join is handled once and the defaults are applied in one order.
/// Hooks that only one default needs stay next to that default.
/// </summary>
public static class NewColonistDefaults
{
    // Faction.IsPlayer is a plain def check. Faction.OfPlayer goes through the world's
    // faction manager, which is not safe to touch while a save is still loading.
    public static bool IsPlayerPawn(Pawn pawn)
    {
        return pawn.Faction != null && pawn.Faction.IsPlayer;
    }

    public struct JoinState
    {
        public bool wasPlayer;
        public bool wasPrisoner;
    }
}

// Pawns that already existed under another faction: recruited prisoners, enslaved pawns,
// rescued refugees. Only a switch into the player faction counts, so a pawn that is merely
// re-assigned to it keeps what the player gave them. Enslaving is the one join that still
// has the pawn as a prisoner here (recruiting clears that first); slaves keep the game's own
// slave medical care default.
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

// Starting colonists only join the player faction directly, without SetFaction, once the map
// is being generated. Vanilla then switches all of their work off and hands each job to
// whichever starting colonist is best at it, which would overwrite the work priorities
// applied when they were created.
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
