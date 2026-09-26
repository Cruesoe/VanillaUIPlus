using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Moves the colonist bar below the dev-mode toolbar while dev mode is on, by shifting its calculated positions so clicks still line up.
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

    // Marks the bar's cached positions dirty when the offset changes, such as on toggling dev mode.
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
    // Resolved by exact signature, since CalculateDrawLocs is overloaded; skipped if it no longer matches.
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
