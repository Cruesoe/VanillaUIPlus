using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

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
