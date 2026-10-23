using System.Reflection;
using HarmonyLib;

namespace VanillaUIPlus;

[HarmonyPatch]
public static class Patch_FPSAndTPS_TPSCounter
{
    public static bool Prepare()
    {
        return AccessTools.TypeByName("FPSAndTPS.TPSCounter") != null && FpsAndTpsDisplay.Bound;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("FPSAndTPS.TPSCounter:Draw");
    }

    public static bool Prefix()
    {
        return !UiPlusMod.Enabled;
    }
}
