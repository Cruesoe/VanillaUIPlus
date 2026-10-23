using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.Init))]
internal static class Patch_MainMenuDrawer_Init
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        TitleScreenOptions.RefreshLatestSave();
    }
}

[HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing))]
internal static class Patch_OptionListingUtility_DrawOptionListing
{
    [HarmonyPrefix]
    private static void Prefix(List<ListableOption> optList)
    {
        TitleScreenOptions.ModifyOptions(optList);
    }
}
