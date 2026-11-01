using System.Reflection;
using HarmonyLib;

namespace VanillaUIPlus;

// Removes the strip Auto Hunt reserves above the table for its toggle.
[HarmonyPatch]
public static class Patch_AutoHunt_WildlifeTabWithToggle_ExtraTopSpace
{
    public static bool Prepare()
    {
        return AutoHuntControls.Init() && AutoHuntControls.TableOnly != null;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.DeclaredPropertyGetter(AutoHuntControls.AutoHuntTabType, "ExtraTopSpace");
    }

    public static void Postfix(ref float __result)
    {
        __result = 0f;
    }
}
