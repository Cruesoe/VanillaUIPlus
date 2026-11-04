using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

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
