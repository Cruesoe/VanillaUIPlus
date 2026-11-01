using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Runs once, when a pawn first becomes able to work for the colony.
[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
public static class Patch_Pawn_WorkSettings_EnableAndInitialize
{
    public static void Postfix(Pawn ___pawn)
    {
        DefaultWorkPriorities.ApplyTo(___pawn);
    }
}
