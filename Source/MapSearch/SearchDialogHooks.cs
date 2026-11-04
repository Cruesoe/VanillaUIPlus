using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Stands in for the search dialogs' DoWindowContents, QuickSearchWidgetRect and WindowUpdate at their non-generic call sites.
/// Patches on Dialog_Search&lt;T&gt; itself stop running on 1.7, so the calls are redirected from Window and WindowStack instead.
/// </summary>
public static class SearchDialogHooks
{
    private const int VanillaTilesPerFrame = 500;
    private const int ExtraWorldTilesPerFrame = 2000;

    public static readonly Func<Window, Rect, Rect, Rect>? QuickSearchRect = ReflectionGuard.Delegate<Func<Window, Rect, Rect, Rect>>(
        nameof(Window), "QuickSearchWidgetRect", AccessTools.Method(typeof(Window), "QuickSearchWidgetRect"));

    public static void DoWindowContents(Window window, Rect inRect)
    {
        if (window is Dialog_MapSearch map && MapSearchUi.MapReady)
        {
            MapSearchUi.DrawMap(map, inRect);
            return;
        }

        if (window is Dialog_WorldSearch world && MapSearchUi.WorldReady)
        {
            MapSearchUi.DrawWorld(world, inRect);
            return;
        }

        window.DoWindowContents(inRect);
    }

    public static Rect QuickSearchWidgetRect(Window window, Rect winRect, Rect inRect)
    {
        Rect rect = QuickSearchRect!(window, winRect, inRect);
        MapSearchUi.AdjustQuickSearchRect(window, ref rect);
        return rect;
    }

    // Points at every item of a hovered map group; runs advanced world searches without typed text and searches more tiles per frame when every tile is searched.
    public static void WindowUpdate(Window window)
    {
        window.WindowUpdate();
        if (window is Dialog_MapSearch)
        {
            MapSearchUi.DrawGroupArrows(window);
            return;
        }

        if (window is not Dialog_WorldSearch world || !MapSearchUi.WorldReady || !MapSearchUi.Searching(world))
        {
            return;
        }

        int perFrame = (SearchDialogAccess<WorldSearchElement>.Searching!(world) ? 0 : VanillaTilesPerFrame)
            + (UiPlusMod.Settings.worldSearchAllTiles ? ExtraWorldTilesPerFrame : 0);
        List<WorldSearchElement> all = SearchDialogAccess<WorldSearchElement>.AllElements!(world);
        ref int index = ref SearchDialogAccess<WorldSearchElement>.SearchIndex!(world);
        for (int i = 0; i < perFrame; i++)
        {
            index++;
            if (index >= all.Count)
            {
                all.Clear();
                break;
            }

            SearchDialogAccess<WorldSearchElement>.TryAddElement!(world, all[index]);
        }
    }
}
