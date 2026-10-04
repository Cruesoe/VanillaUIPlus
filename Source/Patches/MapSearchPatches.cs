using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Dialog_Search<T> code is shared between reference types, so these Dialog_Search<Thing> patches also run for the world search.
[HarmonyPatch(typeof(Dialog_Search<Thing>), nameof(Dialog_Search<Thing>.DoWindowContents))]
public static class Patch_Dialog_Search_DoWindowContents
{
    public static bool Prefix(Window __instance, Rect inRect)
    {
        if (__instance is Dialog_MapSearch map && MapSearchUi.MapReady)
        {
            MapSearchUi.DrawMap(map, inRect);
            return false;
        }

        if (__instance is Dialog_WorldSearch world && MapSearchUi.WorldReady)
        {
            MapSearchUi.DrawWorld(world, inRect);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Dialog_Search<Thing>), "SetInitialSizeAndPosition")]
public static class Patch_Dialog_Search_SetInitialSizeAndPosition
{
    public static void Postfix(Window __instance)
    {
        MapSearchUi.AfterSetSize(__instance);
    }
}

[HarmonyPatch(typeof(Dialog_MapSearch), "SetInitialSizeAndPosition")]
public static class Patch_Dialog_MapSearch_SetInitialSizeAndPosition
{
    public static void Postfix(Window __instance)
    {
        MapSearchUi.AfterSetSize(__instance);
    }
}

[HarmonyPatch(typeof(Dialog_Search<Thing>), "QuickSearchWidgetRect")]
public static class Patch_Dialog_Search_QuickSearchWidgetRect
{
    public static void Postfix(Window __instance, ref Rect __result)
    {
        MapSearchUi.AdjustQuickSearchRect(__instance, ref __result);
    }
}

// Points at every item of a hovered map group; searches more world tiles per frame when every tile is searched.
[HarmonyPatch(typeof(Dialog_Search<Thing>), nameof(Dialog_Search<Thing>.WindowUpdate))]
public static class Patch_Dialog_Search_WindowUpdate
{
    private const int ExtraWorldTilesPerFrame = 2000;

    public static void Postfix(Window __instance)
    {
        if (__instance is Dialog_MapSearch)
        {
            MapSearchUi.DrawGroupArrows(__instance);
            return;
        }

        if (__instance is not Dialog_WorldSearch world || !UiPlusMod.Settings.worldSearchAllTiles || !MapSearchUi.WorldReady
            || !SearchDialogAccess<WorldSearchElement>.Searching!(world))
        {
            return;
        }

        List<WorldSearchElement> all = SearchDialogAccess<WorldSearchElement>.AllElements!(world);
        ref int index = ref SearchDialogAccess<WorldSearchElement>.SearchIndex!(world);
        for (int i = 0; i < ExtraWorldTilesPerFrame; i++)
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

// Vanilla only searches while text is typed; an advanced search runs without it.
[HarmonyPatch(typeof(Dialog_Search<Thing>), "Searching", MethodType.Getter)]
public static class Patch_Dialog_Search_Searching
{
    public static void Postfix(Window __instance, ref bool __result)
    {
        if (__result || __instance is not Dialog_WorldSearch world || !WorldQueryPanel.Active || !SearchDialogAccess<WorldSearchElement>.Ready)
        {
            return;
        }

        List<WorldSearchElement> all = SearchDialogAccess<WorldSearchElement>.AllElements!(world);
        __result = all.Count > 0 && SearchDialogAccess<WorldSearchElement>.SearchIndex!(world) < all.Count;
    }
}

// An advanced search must match, and typed text then narrows it; text alone also looks in the chosen tile details.
[HarmonyPatch(typeof(Dialog_WorldSearch), "ElementMatch")]
public static class Patch_Dialog_WorldSearch_ElementMatch
{
    public static bool Prefix(Dialog_WorldSearch __instance, WorldSearchElement element, ref bool __result)
    {
        bool query = WorldQueryPanel.Active;
        if (!query && !UiPlusMod.Settings.worldSearchAllTiles)
        {
            return true;
        }

        if (query && (!element.tile.Valid || !WorldQueryPanel.Matches(element.tile.Tile)))
        {
            __result = false;
            return false;
        }

        string text = __instance.CommonSearchWidget.filter.Text;
        __result = text.NullOrEmpty() ? query : WorldSearchText.Matches(element, text);
        return false;
    }
}

[HarmonyPatch(typeof(Dialog_WorldSearch), "InitializeSearchSet")]
public static class Patch_Dialog_WorldSearch_InitializeSearchSet
{
    public static void Postfix(List<WorldSearchElement> ___searchSet)
    {
        if (UiPlusMod.Settings.worldSearchAllTiles)
        {
            WorldSearchTiles.AddLandTiles(___searchSet);
        }
    }
}

[HarmonyPatch(typeof(Dialog_WorldSearch), "SearchSet", MethodType.Getter)]
public static class Patch_Dialog_WorldSearch_SearchSet
{
    public static void Postfix(ref List<WorldSearchElement> __result)
    {
        if (WorldSearchRange.ShouldLimit)
        {
            __result = WorldSearchRange.Filter(__result);
        }
    }
}

// Plain tiles have no place, landmark or feature to name them, so they show their biome.
[HarmonyPatch(typeof(WorldSearchElement), nameof(WorldSearchElement.DisplayLabel), MethodType.Getter)]
public static class Patch_WorldSearchElement_DisplayLabel
{
    public static void Postfix(WorldSearchElement __instance, ref string? __result)
    {
        if (__result == null && __instance.tile.Valid)
        {
            __result = __instance.tile.Tile.PrimaryBiome.LabelCap;
        }
    }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawWorldRadiusRing))]
public static class Patch_GenDraw_DrawWorldRadiusRing
{
    public static void Prefix(PlanetTile center, int radius)
    {
        WorldSearchRange.NotifyRingDrawn(center, radius);
    }
}
