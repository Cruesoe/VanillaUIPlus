using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoTimespeedControls))]
public static class Patch_DoTimespeedControls
{
    public static bool Prefix(ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        TimeSpeedControls.Draw(ref curBaseY);
        DubsTpsDisplay.Draw(ref curBaseY);
        FpsAndTpsDisplay.Draw(ref curBaseY);
        return false;
    }
}
