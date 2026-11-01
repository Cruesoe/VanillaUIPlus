using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

// Auto Hunt's Wildlife tab draws only the table, without its top-left toggle; the toggle is redrawn on the bottom row by the patch above.
[HarmonyPatch]
public static class Patch_AutoHunt_WildlifeTabWithToggle_DoWindowContents
{
    public static bool Prepare()
    {
        return AutoHuntControls.Init() && AutoHuntControls.TableOnly != null;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.DeclaredMethod(AutoHuntControls.AutoHuntTabType, "DoWindowContents");
    }

    public static bool Prefix(MainTabWindow_PawnTable __instance, Rect rect)
    {
        AutoHuntControls.TableOnly!(__instance, rect);
        return false;
    }
}
