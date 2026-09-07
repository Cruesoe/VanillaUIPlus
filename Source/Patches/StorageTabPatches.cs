using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// ITab_Storage sizes its outer window from the protected instance field "size" (declared
// on the ITab base) but sizes its inner content rect from its own private static
// "WinSize" field directly in FillTab - patching only "size" grows the window without
// growing what is drawn inside it. Both are overridden together here, in a Prefix on
// FillTab so it runs every frame the tab is open and reacts immediately to the setting
// changing, without needing the tab to be closed and reopened.
[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
public static class Patch_ITab_Storage_FillTab
{
    private static readonly FieldInfo? SizeField = AccessTools.Field(typeof(ITab), "size");
    private static readonly FieldInfo? WinSizeField = AccessTools.Field(typeof(ITab_Storage), "WinSize");

    private static readonly AccessTools.FieldRef<ITab, Vector2>? Size =
        ReflectionGuard.FieldRef<ITab, Vector2>(nameof(ITab_Storage), "size", SizeField);
    private static readonly AccessTools.FieldRef<Vector2>? WinSize =
        ReflectionGuard.StaticFieldRef<Vector2>(nameof(ITab_Storage), "WinSize", WinSizeField);

    // Captured once, before this patch ever writes to the field, so it stays the true
    // vanilla default to restore when the setting is off.
    private static readonly Vector2 VanillaWinSize = WinSizeField != null ? (Vector2)WinSizeField.GetValue(null) : new Vector2(300f, 480f);

    public static void Prefix(ITab_Storage __instance)
    {
        if (Size == null || WinSize == null)
        {
            return;
        }

        Vector2 target = UiPlusMod.Settings.resizeFilterTab
            ? new Vector2(UiPlusMod.Settings.filterTabWidth, UiPlusMod.Settings.filterTabHeight)
            : VanillaWinSize;

        WinSize() = target;
        Size(__instance) = target;
    }
}

// Vanilla force-opens the first root category (Food, by def-load order) once when the
// thing category tree is built - ThingCategoryDef.treeNode is a single shared instance
// per category used by every filter UI in the game (storage, bills, outfits, etc.), so
// that one category then starts expanded everywhere, every session, while its siblings
// start collapsed. FinalizeInit runs once at startup, so closing every node here after it
// runs is enough; no per-frame patch is needed.
[HarmonyPatch(typeof(ThingCategoryNodeDatabase), nameof(ThingCategoryNodeDatabase.FinalizeInit))]
public static class Patch_ThingCategoryNodeDatabase_FinalizeInit
{
    public static void Postfix()
    {
        if (!UiPlusMod.Settings.collapseFilterCategoriesByDefault || ThingCategoryNodeDatabase.allThingCategoryNodes == null)
        {
            return;
        }

        foreach (TreeNode_ThingCategory node in ThingCategoryNodeDatabase.allThingCategoryNodes)
        {
            node.SetOpen(-1, false);
        }
    }
}
