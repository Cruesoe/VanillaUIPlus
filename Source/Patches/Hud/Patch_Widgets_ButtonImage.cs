using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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
