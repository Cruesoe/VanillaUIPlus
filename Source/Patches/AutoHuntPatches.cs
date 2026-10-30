using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

// Patching Numbers' DoWindowContents would run its static constructor, which reads Find.World and throws at startup; its override calls this base method, so that is patched instead.
[HarmonyPatch]
public static class Patch_MainTabWindow_PawnTable_DoWindowContents_AutoHunt
{
    public static bool Prepare()
    {
        return AutoHuntControls.Init();
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.DeclaredMethod(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.DoWindowContents));
    }

    public static void Postfix(MainTabWindow_PawnTable __instance, Rect rect)
    {
        if (Event.current.type != EventType.Layout)
        {
            AutoHuntControls.Draw(__instance, rect);
        }
    }
}

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
