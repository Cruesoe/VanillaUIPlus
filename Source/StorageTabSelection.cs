using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Collapses filter categories at game start and focuses the storage tab's search box whenever a different stockpile is shown.
public static class StorageTabSelection
{
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
        if (!UiPlusMod.Settings.collapseFilterCategoriesByDefault || ThingCategoryNodeDatabase.allThingCategoryNodes == null)
        {
            return;
        }

        foreach (TreeNode_ThingCategory node in ThingCategoryNodeDatabase.allThingCategoryNodes)
        {
            node.SetOpen(-1, false);
        }
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
}
