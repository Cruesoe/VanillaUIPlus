using HarmonyLib;
using RimWorld;

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
