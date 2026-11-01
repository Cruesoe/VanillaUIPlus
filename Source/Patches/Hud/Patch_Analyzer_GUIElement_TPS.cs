using System.Reflection;
using HarmonyLib;

namespace VanillaUIPlus;

[HarmonyPatch]
public static class Patch_Analyzer_GUIElement_TPS
{
    public static bool Prepare()
    {
        return AccessTools.TypeByName("Analyzer.GUIElement_TPS") != null;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("Analyzer.GUIElement_TPS:Prefix");
    }

    public static bool Prefix()
    {
        return !UiPlusMod.Enabled;
    }
}
