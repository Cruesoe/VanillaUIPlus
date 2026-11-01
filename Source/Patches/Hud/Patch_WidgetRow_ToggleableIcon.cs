using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
public static class Patch_WidgetRow_ToggleableIcon
{
    public static bool Prefix(Texture2D tex, string tooltip)
    {
        return !PlayButtonFilter.Filtering || PlayButtonFilter.Allow(tex, tooltip);
    }
}
