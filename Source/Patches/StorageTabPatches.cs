using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Sets both the tab's window size and its static content size each draw, so the setting applies immediately.
[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
public static class Patch_ITab_Storage_FillTab
{
    private static readonly FieldInfo? SizeField = AccessTools.Field(typeof(ITab), "size");
    private static readonly FieldInfo? WinSizeField = AccessTools.Field(typeof(ITab_Storage), "WinSize");

    private static readonly AccessTools.FieldRef<ITab, Vector2>? Size =
        ReflectionGuard.FieldRef<ITab, Vector2>(nameof(ITab_Storage), "size", SizeField);
    private static readonly AccessTools.FieldRef<Vector2>? WinSize =
        ReflectionGuard.StaticFieldRef<Vector2>(nameof(ITab_Storage), "WinSize", WinSizeField);

    // Vanilla's size, captured before the first write, restored when the setting is off.
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

// Collapses the storage tab's categories (its own open bit only) and focuses its search box whenever a different stockpile is shown.
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

    // Retried for a few frames, since focus only sticks once the field has been drawn.
    private const int FocusAttempts = 5;
    private static int focusAttemptsLeft;

    // Called after patching, since the tree is first built before patches exist.
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

// Reopening the tab counts as a new stockpile.
[HarmonyPatch(typeof(ITab_Storage), nameof(ITab_Storage.OnOpen))]
public static class Patch_ITab_Storage_OnOpen
{
    public static void Postfix()
    {
        StorageTabSelection.Forget();
    }
}

// Collapses again when a def reload rebuilds the tree.
[HarmonyPatch(typeof(ThingCategoryNodeDatabase), nameof(ThingCategoryNodeDatabase.FinalizeInit))]
public static class Patch_ThingCategoryNodeDatabase_FinalizeInit
{
    public static void Postfix()
    {
        StorageTabSelection.CollapseAll();
    }
}
