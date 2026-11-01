using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

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
