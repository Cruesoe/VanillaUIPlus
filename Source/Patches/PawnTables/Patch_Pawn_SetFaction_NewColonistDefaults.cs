using HarmonyLib;
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
