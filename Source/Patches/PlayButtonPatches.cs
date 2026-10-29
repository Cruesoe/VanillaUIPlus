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

    // Clears the filter if the original throws, or icons would stay hidden across the UI.
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

// Draws play-setting icons through IconFit so small glyphs fill the button like the vanilla ones.
[HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonImage), typeof(Rect), typeof(Texture2D), typeof(Color), typeof(Color), typeof(bool), typeof(string))]
public static class Patch_Widgets_ButtonImage
{
    public static bool Prefix(Rect butRect, Texture2D tex, Color baseColor, Color mouseoverColor, bool doMouseoverSound, string tooltip, ref bool __result)
    {
        if (!PlayButtonFilter.IsFitted(tex))
        {
            return true;
        }

        GUI.color = Mouse.IsOver(butRect) ? mouseoverColor : baseColor;
        IconFit.Draw(butRect, tex);
        GUI.color = baseColor;
        if (!tooltip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(butRect, tooltip);
        }

        __result = Widgets.ButtonInvisible(butRect, doMouseoverSound);
        GUI.color = Color.white;
        return false;
    }
}
