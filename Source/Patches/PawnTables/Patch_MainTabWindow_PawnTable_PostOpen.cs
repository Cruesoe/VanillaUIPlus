using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// MainTabWindow_Animals doesn't override PostOpen, so the base method is patched and filtered to the Animals tab.
[HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.PostOpen))]
public static class Patch_MainTabWindow_PawnTable_PostOpen
{
    public static void Prefix(MainTabWindow_PawnTable __instance)
    {
        if (__instance is MainTabWindow_Animals)
        {
            AnimalGroups.CollapseAll();
        }
    }
}
