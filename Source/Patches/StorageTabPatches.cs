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
        StorageTabSelection.OnStorageTabDrawn(__instance);
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

    public static void Postfix(ITab_Storage __instance)
    {
        StorageTabSelection.AfterStorageTabDrawn(__instance);
    }
}

// Vanilla force-opens the first root category (Food, by def-load order) when the thing
// category tree is built. ThingCategoryDef.treeNode is a single shared instance per
// category used by every filter UI in the game (storage, bills, outfits, etc.), so that
// category starts expanded everywhere, and anything opened on one stockpile is still open
// on the next. The storage tab uses its own open-state bit, so clearing that bit whenever
// a different stockpile is shown starts each one collapsed without touching other trees.
// The same moment is used to put the cursor in the tab's search box.
public static class StorageTabSelection
{
    // Vanilla ITab_Storage passes this openMask to ThingFilterUI.
    private const int StorageOpenMask = 8;

    private static readonly System.Func<ITab_Storage, IStoreSettingsParent>? SelStoreSettingsParent =
        ReflectionGuard.Delegate<System.Func<ITab_Storage, IStoreSettingsParent>>(
            nameof(ITab_Storage), "SelStoreSettingsParent",
            AccessTools.PropertyGetter(typeof(ITab_Storage), "SelStoreSettingsParent"));

    // Weak so a removed stockpile is not kept alive by the last one looked at.
    private static readonly System.WeakReference<IStoreSettingsParent?> LastShown =
        new System.WeakReference<IStoreSettingsParent?>(null);

    private static readonly FieldInfo? ThingFilterStateField = AccessTools.Field(typeof(ITab_Storage), "thingFilterState");
    private static readonly AccessTools.FieldRef<ITab_Storage, ThingFilterUI.UIState>? ThingFilterState =
        ReflectionGuard.FieldRef<ITab_Storage, ThingFilterUI.UIState>(nameof(ITab_Storage), "thingFilterState", ThingFilterStateField);

    // Focus only sticks once the search field has been drawn, so it is retried for a few
    // frames after a new stockpile is shown rather than tried once and lost.
    private const int FocusAttempts = 5;
    private static int focusAttemptsLeft;

    // Vanilla's forced open happens while defs load, before any Harmony patch exists, so it
    // is undone here once patching is done rather than in a FinalizeInit postfix.
    public static void CollapseAll()
    {
        Collapse(-1);
    }

    public static void Forget()
    {
        LastShown.SetTarget(null);
    }

    // Called before the tab draws.
    public static void OnStorageTabDrawn(ITab_Storage tab)
    {
        if (SelStoreSettingsParent == null)
        {
            return;
        }

        IStoreSettingsParent? current = SelStoreSettingsParent(tab);
        if (current == null || (LastShown.TryGetTarget(out IStoreSettingsParent? last) && ReferenceEquals(last, current)))
        {
            return;
        }

        LastShown.SetTarget(current);
        Collapse(StorageOpenMask);
        focusAttemptsLeft = UiPlusMod.Settings.focusStorageSearch ? FocusAttempts : 0;
    }

    // Called after the tab draws, once the search field exists.
    public static void AfterStorageTabDrawn(ITab_Storage tab)
    {
        if (focusAttemptsLeft <= 0 || ThingFilterState == null)
        {
            return;
        }

        QuickSearchWidget search = ThingFilterState(tab).quickSearch;
        if (search.CurrentlyFocused())
        {
            focusAttemptsLeft = 0;
            return;
        }

        focusAttemptsLeft--;
        search.Focus();
    }

    private static void Collapse(int mask)
    {
        if (!UiPlusMod.Settings.collapseFilterCategoriesByDefault || ThingCategoryNodeDatabase.allThingCategoryNodes == null)
        {
            return;
        }

        foreach (TreeNode_ThingCategory node in ThingCategoryNodeDatabase.allThingCategoryNodes)
        {
            node.SetOpen(mask, false);
        }
    }
}

// Reopening the tab counts as a fresh look, even on the same stockpile, so categories
// collapse and the search box is selected again.
[HarmonyPatch(typeof(ITab_Storage), nameof(ITab_Storage.OnOpen))]
public static class Patch_ITab_Storage_OnOpen
{
    public static void Postfix()
    {
        StorageTabSelection.Forget();
    }
}

// Defs are reloaded when the language or mod list changes, which rebuilds the tree and
// opens the first category again; the patch exists by then.
[HarmonyPatch(typeof(ThingCategoryNodeDatabase), nameof(ThingCategoryNodeDatabase.FinalizeInit))]
public static class Patch_ThingCategoryNodeDatabase_FinalizeInit
{
    public static void Postfix()
    {
        StorageTabSelection.CollapseAll();
    }
}
