using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Draws a category's children in the saved order as part of the joined drag set.
[HarmonyPatch(typeof(Listing_ResourceReadout), nameof(Listing_ResourceReadout.DoCategoryChildren))]
public static class Patch_Listing_ResourceReadout_DoCategoryChildren
{
    public static bool Prefix(Listing_ResourceReadout __instance, TreeNode_ThingCategory node, int indentLevel, int openMask)
    {
        if (ResourceReadoutTweaks.DoThingDef == null)
        {
            return true;
        }

        ResourceReadoutTweaks.DrawChildren(__instance, node, indentLevel, openMask);
        return false;
    }
}
