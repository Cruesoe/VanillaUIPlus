using System.Collections.Generic;
using System.Reflection;
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
    private static float appliedOffset = float.NaN;

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

    /// <summary>
    /// The bar caches its positions and only rebuilds them when its entries change, so
    /// toggling development mode would otherwise leave the previous offset baked in until
    /// something unrelated happened to invalidate the cache. Marking it dirty on a change
    /// makes the shift appear and disappear with development mode.
    /// </summary>
    public static void RefreshIfChanged()
    {
        float offset = CurrentOffset;
        if (offset == appliedOffset)
        {
            return;
        }

        appliedOffset = offset;
        Find.ColonistBar?.MarkColonistsDirty();
    }
}

[HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
public static class Patch_ColonistBar_ColonistBarOnGUI
{
    public static void Prefix()
    {
        ColonistBarOffset.RefreshIfChanged();
    }
}

[HarmonyPatch]
public static class Patch_ColonistBarDrawLocsFinder_CalculateDrawLocs
{
    // CalculateDrawLocs is overloaded and one of the overloads is not public, so naming
    // the method alone is ambiguous to Harmony and throws while patching. Resolve it by
    // exact signature instead, and skip the patch rather than throw if it stops matching.
    public static MethodBase? TargetMethod()
    {
        return AccessTools.Method(
            typeof(ColonistBarDrawLocsFinder),
            nameof(ColonistBarDrawLocsFinder.CalculateDrawLocs),
            new[] { typeof(List<Vector2>), typeof(float).MakeByRefType(), typeof(int) });
    }

    public static bool Prepare()
    {
        return TargetMethod() != null;
    }

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
