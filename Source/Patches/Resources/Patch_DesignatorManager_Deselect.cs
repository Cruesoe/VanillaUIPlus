using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
public static class Patch_DesignatorManager_Deselect
{
    public static void Postfix(DesignatorManager __instance)
    {
        DesignatorOverlay.DeselectedAll(__instance);
    }
}
