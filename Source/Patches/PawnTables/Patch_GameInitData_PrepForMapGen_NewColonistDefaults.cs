using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Starting colonists get the default schedule and assignments; their work priorities keep the game's skill-based split, set here.
[HarmonyPatch(typeof(GameInitData), nameof(GameInitData.PrepForMapGen))]
public static class Patch_GameInitData_PrepForMapGen_NewColonistDefaults
{
    public static void Postfix(GameInitData __instance)
    {
        foreach (Pawn pawn in __instance.startingAndOptionalPawns)
        {
            DefaultSchedule.ApplyTo(pawn);
            DefaultAssignments.ApplyTo(pawn);
        }
    }
}
