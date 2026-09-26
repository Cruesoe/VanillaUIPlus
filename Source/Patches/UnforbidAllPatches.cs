using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Reads the hotkeys first each frame, ahead of other mods' map GUI handlers, and uses the event.
[HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_BeforeMainTabs))]
[HarmonyPriority(Priority.First)]
public static class Patch_MapInterface_MapInterfaceOnGUI_BeforeMainTabs
{
    public static void Prefix()
    {
        UnforbidAllHotkey.HandleKeys();
        TemperatureOverlayHotkey.HandleKeys();
    }
}
