using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Toggles development mode from the main menu or in game; UIRootOnGUI only runs once the UI exists.
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
public static class Patch_UIRoot_UIRootOnGUI_DevModeHotkey
{
    [HarmonyPostfix]
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
        }
    }
}
