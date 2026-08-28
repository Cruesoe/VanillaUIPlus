using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
public static class Patch_PlaySettings_DoPlaySettingsGlobalControls
{
    public static void Prefix(bool worldView)
    {
        if (!UiPlusMod.Enabled)
        {
            return;
        }

        PlayButtonFilter.Begin(worldView);
    }

    public static void Postfix(WidgetRow row, bool worldView)
    {
        PlayButtonFilter.DrawSettingsButton(row, worldView);
        if (PlayButtonFilter.Filtering)
        {
            PlayButtonFilter.End();
        }
    }

    // The WidgetRow patches below key off PlayButtonFilter.Filtering, which is set for
    // the duration of this method only. If the original throws, Postfix never runs and a
    // stuck flag would hide icons across the whole UI, so clear it here as well.
    public static void Finalizer()
    {
        PlayButtonFilter.Abort();
    }
}

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
public static class Patch_WidgetRow_ToggleableIcon
{
    public static bool Prefix(Texture2D tex, string tooltip)
    {
        return !PlayButtonFilter.Filtering || PlayButtonFilter.Allow(tex, tooltip);
    }
}

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ButtonIcon))]
public static class Patch_WidgetRow_ButtonIcon
{
    public static bool Prefix(Texture2D tex, string tooltip, ref bool __result)
    {
        if (!PlayButtonFilter.Filtering || PlayButtonFilter.Allow(tex, tooltip))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
