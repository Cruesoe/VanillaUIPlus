using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// A shortcut for vanilla's temperature overlay; inactive while Heat Map, which has its own, is loaded.
public static class TemperatureOverlayHotkey
{
    private const string HeatMapPackageId = "Syrus.HeatMap";

    // The active mod list cannot change without a restart, so this is resolved once.
    private static bool? heatMapActive;

    public static bool HeatMapActive => heatMapActive ??= ModsConfig.IsActive(HeatMapPackageId);

    public static void HandleKeys()
    {
        if (!UiPlusMod.Settings.enableTemperatureOverlayHotkey || HeatMapActive)
        {
            return;
        }

        if (Find.CurrentMap == null || !WorldRendererUtility.DrawingMap || Event.current.type != EventType.KeyDown || !VUIPDefOf.VUIP_ToggleTemperatureOverlay.KeyDownEvent)
        {
            return;
        }

        Event.current.Use();
        Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
    }
}
