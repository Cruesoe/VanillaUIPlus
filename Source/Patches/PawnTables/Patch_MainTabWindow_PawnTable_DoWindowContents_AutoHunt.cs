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
