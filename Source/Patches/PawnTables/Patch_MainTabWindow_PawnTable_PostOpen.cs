using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// The Animals and Wildlife tabs don't override PostOpen, so the base method is patched; only one main tab is open at a time, so every species starts collapsed.
[HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.PostOpen))]
public static class Patch_MainTabWindow_PawnTable_PostOpen
{
    public static void Prefix()
    {
        AnimalGroups.CollapseAll();
    }
}
