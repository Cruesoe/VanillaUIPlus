using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// A Prefix with top priority: MapInterfaceOnGUI_BeforeMainTabs is the first thing RimWorld's
// UIRootOnGUI calls each frame, and everything else that might also be listening for this
// key - other mods' MapComponentOnGUI handlers included - runs from further down inside that
// same call. Unity flips a used Event's type to EventType.Used, so whichever handler reads
// the keypress first is the one that gets it. Going first here, ahead of the method this
// keypress would otherwise be read inside of, is what makes this take priority.
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
