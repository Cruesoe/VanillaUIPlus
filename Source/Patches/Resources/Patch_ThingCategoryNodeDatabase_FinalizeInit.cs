using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Collapses again when a def reload rebuilds the tree, then reopens the saved readout categories.
[HarmonyPatch(typeof(ThingCategoryNodeDatabase), nameof(ThingCategoryNodeDatabase.FinalizeInit))]
public static class Patch_ThingCategoryNodeDatabase_FinalizeInit
{
    public static void Postfix()
    {
        StorageTabSelection.CollapseAll();
        ResourceReadoutTweaks.RestoreOpenCategories();
    }
}
