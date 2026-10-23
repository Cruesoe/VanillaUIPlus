using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

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
