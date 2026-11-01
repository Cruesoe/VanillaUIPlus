using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Runs before Heat Map's postfix on the same method, which toggles the same overlay.
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
[HarmonyPriority(Priority.First)]
public static class Patch_UIRoot_UIRootOnGUI_TemperatureOverlayHotkey
{
    public static void Postfix()
    {
        TemperatureOverlayHotkey.HandleKeys();
    }
}
