using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Select))]
public static class Patch_DesignatorManager_Select
{
    public static void Postfix(DesignatorManager __instance)
    {
        GrowingZoneFertility.Selected(__instance);
        BuildTerrainAffordance.Selected(__instance);
    }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
public static class Patch_DesignatorManager_Deselect
{
    public static void Postfix(DesignatorManager __instance)
    {
        GrowingZoneFertility.Deselected(__instance);
        BuildTerrainAffordance.Deselected(__instance);
    }
}
