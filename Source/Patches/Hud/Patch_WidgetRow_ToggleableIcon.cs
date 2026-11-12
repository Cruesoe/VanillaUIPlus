using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
public static class Patch_WidgetRow_ToggleableIcon
{
    // In the play-settings row: sets a new game's default state (even for hidden toggles), then filters hidden ones out.
    public static bool Prefix(ref bool toggleable, Texture2D tex, string tooltip)
    {
        if (!PlayButtonFilter.Filtering)
        {
            return true;
        }

        bool allow = PlayButtonFilter.Allow(tex, tooltip);
        if (tex != null)
        {
            PlayButtonDefaults.Apply(PlayButtonFilter.ToggleId(tex), ref toggleable);
        }

        return allow;
    }
}
