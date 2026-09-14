using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

/// <summary>
/// Gives the Bio tab one extra row of vertical space. Vanilla calculates the outer
/// inspect tab and the character card drawn inside it separately, so both paths need
/// the same adjustment or the left-hand scrollbar remains.
/// </summary>
public static class CharacterTabSize
{
    public const float ExtraHeight = 20f;

    public static Vector2 Expand(Vector2 size)
    {
        size.y += ExtraHeight;
        return size;
    }
}

[HarmonyPatch(typeof(ITab_Pawn_Character), "UpdateSize")]
public static class Patch_ITab_Pawn_Character_UpdateSize
{
    private static readonly FieldInfo? SizeField = AccessTools.Field(typeof(ITab), "size");
    private static readonly AccessTools.FieldRef<ITab, Vector2>? Size =
        ReflectionGuard.FieldRef<ITab, Vector2>(nameof(ITab_Pawn_Character), "size", SizeField);

    public static void Postfix(ITab_Pawn_Character __instance)
    {
        if (Size == null)
        {
            return;
        }

        Size(__instance) = CharacterTabSize.Expand(Size(__instance));
    }
}

[HarmonyPatch(typeof(ITab_Pawn_Character), "FillTab")]
public static class Patch_ITab_Pawn_Character_FillTab
{
    private static readonly MethodInfo PawnCardSizeMethod =
        AccessTools.Method(typeof(CharacterCardUtility), nameof(CharacterCardUtility.PawnCardSize));
    private static readonly MethodInfo ExpandMethod =
        AccessTools.Method(typeof(CharacterTabSize), nameof(CharacterTabSize.Expand));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(PawnCardSizeMethod))
            {
                yield return new CodeInstruction(OpCodes.Call, ExpandMethod);
            }
        }
    }
}
