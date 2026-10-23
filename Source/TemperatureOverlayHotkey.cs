using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Toggles vanilla's temperature overlay.
public static class TemperatureOverlayHotkey
{
    public static void HandleKeys()
    {
        if (!UiPlusMod.Settings.enableTemperatureOverlayHotkey)
        {
            return;
        }

        if (Find.CurrentMap == null || !WorldRendererUtility.DrawingMap || Event.current.type != EventType.KeyDown || !Pressed(VUIPDefOf.VUIP_ToggleTemperatureOverlay))
        {
            return;
        }

        // Using the event stops Heat Map's handler from toggling the overlay back.
        Event.current.Use();
        Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
    }

    // 1.7 reads Backslash as the US key position ('#' on UK layouts), so a Backslash binding matches the typed '\' instead.
    private static bool Pressed(KeyBindingDef def)
    {
        if (!KeyPrefs.KeyPrefsData.keyPrefs.TryGetValue(def, out KeyBindingData? binding))
        {
            return false;
        }

        if (Event.current.keyCode != KeyCode.Backslash && def.KeyDownEvent)
        {
            return true;
        }

        bool boundToBackslash = binding.keyBindingA == KeyCode.Backslash || binding.keyBindingB == KeyCode.Backslash;
        return boundToBackslash && Event.current.character == '\\' && !Find.WindowStack.AnySearchWidgetFocused;
    }
}
