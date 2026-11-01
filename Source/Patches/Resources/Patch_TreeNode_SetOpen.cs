using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Remembers which readout categories are open, so they stay open after a restart.
[HarmonyPatch(typeof(TreeNode), nameof(TreeNode.SetOpen))]
public static class Patch_TreeNode_SetOpen
{
    public static void Postfix(TreeNode __instance, int mask, bool val)
    {
        if (mask == ResourceReadoutTweaks.CategorizedOpenMask && __instance is TreeNode_ThingCategory node)
        {
            ResourceReadoutTweaks.OnCategoryToggled(node, val);
        }
    }
}
