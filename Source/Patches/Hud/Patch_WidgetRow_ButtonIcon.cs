using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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

    // ButtonIcon advances without the row's gap, unlike ToggleableIcon; adds it so later icons stay on the grid.
    public static void Postfix(WidgetRow __instance, bool __runOriginal)
    {
        if (__runOriginal && PlayButtonFilter.Filtering)
        {
            __instance.Gap(__instance.CellGap);
        }
    }
}
