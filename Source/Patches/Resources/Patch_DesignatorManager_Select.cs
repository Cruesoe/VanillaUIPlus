using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Select))]
public static class Patch_DesignatorManager_Select
{
    public static void Postfix(DesignatorManager __instance)
    {
        DesignatorOverlay.SelectedAll(__instance);
    }
}
