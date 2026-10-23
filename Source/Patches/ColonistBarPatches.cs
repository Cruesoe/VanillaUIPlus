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

// Colony Groups draws its own bar from TacticalGroupsSettings.ColonistBarPositionY, so the offset is added there for the duration of its layout pass.
[HarmonyPatch]
public static class Patch_TacticalGroups_ColonistBarDrawLocsFinder_CalculateDrawLocs
{
    private static FieldInfo? positionYField;

    public static MethodBase? TargetMethod()
    {
        return AccessTools.Method(
            AccessTools.TypeByName("TacticalGroups.ColonistBarDrawLocsFinder"),
            "CalculateDrawLocs",
            new[] { typeof(List<Rect>), typeof(float).MakeByRefType() });
    }

    public static bool Prepare()
    {
        positionYField = AccessTools.Field(AccessTools.TypeByName("TacticalGroups.TacticalGroupsSettings"), "ColonistBarPositionY");
        return positionYField?.FieldType == typeof(float) && TargetMethod() != null;
    }

    public static void Prefix(out float __state)
    {
        __state = float.NaN;
        float offset = ColonistBarOffset.CurrentOffset;
        if (offset <= 0f || positionYField?.GetValue(null) is not float positionY)
        {
            return;
        }

        __state = positionY;
        positionYField.SetValue(null, __state + offset);
    }

    public static void Finalizer(float __state)
    {
        if (!float.IsNaN(__state))
        {
            positionYField!.SetValue(null, __state);
        }
    }
}
