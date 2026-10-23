using System.Reflection;
using HarmonyLib;

namespace VanillaUIPlus;

[HarmonyPatch]
public static class Patch_CombatExtended_DoWindGUI
{
    public static bool Prepare()
    {
        return CombatExtendedWind.Available && TargetMethod() != null;
    }

    public static MethodBase? TargetMethod()
    {
        return AccessTools.Method("CombatExtended.WeatherTracker:DoWindGUI");
    }

    public static bool Prefix(object __instance, ref float yPos)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        CombatExtendedWind.Draw(__instance, ref yPos);
        return false;
    }
}
