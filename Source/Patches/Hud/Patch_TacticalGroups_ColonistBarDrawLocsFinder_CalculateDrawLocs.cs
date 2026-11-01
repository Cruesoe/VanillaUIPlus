using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VanillaUIPlus;

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
