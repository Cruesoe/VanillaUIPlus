using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoPlaySettings))]
public static class Patch_DoPlaySettings
{
    public static bool Prefix(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        TimeSpeedControls.ResetDrewThisGui();
        ReadoutDrawer.DrawPlaySettings(rowVisibility, worldView, ref curBaseY);
        return false;
    }
}
