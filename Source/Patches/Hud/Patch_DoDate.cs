using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))]
public static class Patch_DoDate
{
    public static bool Prefix(ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        ReadoutDrawer.DrawDate(ref curBaseY);
        return false;
    }
}
