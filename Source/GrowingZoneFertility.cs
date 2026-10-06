using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

public static class GrowingZoneFertility
{
    private static DesignatorManager? owner;
    private static PlaySettings? playSettings;
    private static bool previousOverlay;

    public static void Selected(DesignatorManager manager)
    {
        if (!UiPlusMod.Settings.autoGrowingZoneFertility || manager.SelectedDesignator is not Designator_ZoneAdd_Growing
            || Find.CurrentMap == null)
        {
            return;
        }

        Restore();
        owner = manager;
        playSettings = Find.PlaySettings;
        previousOverlay = playSettings.showFertilityOverlay;
        playSettings.showFertilityOverlay = true;
    }

    public static void Deselected(DesignatorManager manager)
    {
        if (owner == manager)
        {
            Restore();
        }
    }

    public static void Restore()
    {
        if (playSettings != null)
        {
            playSettings.showFertilityOverlay = previousOverlay;
        }

        owner = null;
        playSettings = null;
    }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Select))]
public static class Patch_DesignatorManager_Select
{
    public static void Postfix(DesignatorManager __instance)
    {
        GrowingZoneFertility.Selected(__instance);
    }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
public static class Patch_DesignatorManager_Deselect
{
    public static void Postfix(DesignatorManager __instance)
    {
        GrowingZoneFertility.Deselected(__instance);
    }
}
