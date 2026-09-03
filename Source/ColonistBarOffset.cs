using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// With development mode on, its toolbar sits across the top of the screen in the same
/// place as the colonist bar and covers the first row of portraits. This nudges the
/// colonist bar down far enough to clear it, and only while development mode is on.
///
/// The shift is applied to the positions the bar calculates rather than to the drawing,
/// so clicking a portrait still lands where it looks.
/// </summary>
public static class ColonistBarOffset
{
    public static float CurrentOffset
    {
        get
        {
            if (!Prefs.DevMode || !UiPlusMod.Settings.shiftColonistBarInDevMode)
            {
                return 0f;
            }

            return Mathf.Max(0f, UiPlusMod.Settings.colonistBarDevOffset);
        }
    }
}

[HarmonyPatch(typeof(ColonistBarDrawLocsFinder), nameof(ColonistBarDrawLocsFinder.CalculateDrawLocs))]
public static class Patch_ColonistBarDrawLocsFinder_CalculateDrawLocs
{
    public static void Postfix(List<Vector2> outDrawLocs)
    {
        float offset = ColonistBarOffset.CurrentOffset;
        if (offset <= 0f || outDrawLocs == null)
        {
            return;
        }

        for (int i = 0; i < outDrawLocs.Count; i++)
        {
            Vector2 loc = outDrawLocs[i];
            loc.y += offset;
            outDrawLocs[i] = loc;
        }
    }
}
