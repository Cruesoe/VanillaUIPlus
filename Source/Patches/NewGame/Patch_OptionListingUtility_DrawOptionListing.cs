using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing))]
internal static class Patch_OptionListingUtility_DrawOptionListing
{
    [HarmonyPrefix]
    private static void Prefix(List<ListableOption> optList)
    {
        TitleScreenOptions.ModifyOptions(optList);
    }
}
