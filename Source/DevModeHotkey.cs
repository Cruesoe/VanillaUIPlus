using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Toggles development mode from the main menu or in game; UIRootOnGUI only runs once the UI exists.
// Runs before the UI and consumes the key, so other keybinds on the same key (Pawn Editor's) don't also fire.
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
public static class Patch_UIRoot_UIRootOnGUI_DevModeHotkey
{
    [HarmonyPrefix]
    public static void ToggleDevModeOnHotkey()
    {
        if (!UiPlusMod.Settings.enableDevModeHotkey)
        {
            return;
        }

        Event? ev = Event.current;
        if (ev is null || ev.type != EventType.KeyDown || ev.keyCode == KeyCode.None)
        {
            return;
        }

        KeyBindingDef? def = VUIPDefOf.VUIP_ToggleDevMode;
        if (def is null || KeyPrefs.KeyPrefsData?.keyPrefs is null || Find.WindowStack is null)
        {
            return;
        }

        if (def.KeyDownEvent)
        {
            Prefs.DevMode = !Prefs.DevMode;
            // The setter doesn't persist; write Prefs.xml so the toggle survives a restart.
            Prefs.Save();
            ev.Use();
        }
    }
}
