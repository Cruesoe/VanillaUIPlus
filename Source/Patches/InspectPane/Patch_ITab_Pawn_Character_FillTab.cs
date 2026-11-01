using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(ITab_Pawn_Character), "FillTab")]
public static class Patch_ITab_Pawn_Character_FillTab
{
    private static readonly MethodInfo? PawnCardSizeMethod =
        AccessTools.Method(typeof(CharacterCardUtility), nameof(CharacterCardUtility.PawnCardSize));
    private static readonly MethodInfo? ExpandMethod =
        AccessTools.Method(typeof(CharacterTabSize), nameof(CharacterTabSize.Expand), new[] { typeof(Vector2) });

    public static void Prefix()
    {
        CharacterTabSize.DrawingPawn = CharacterTabSize.SelectedPawn();
    }

    public static void Finalizer()
    {
        CharacterTabSize.DrawingPawn = null;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (!ReflectionGuard.Found(nameof(CharacterCardUtility), nameof(CharacterCardUtility.PawnCardSize), PawnCardSizeMethod)
            || !ReflectionGuard.Found(nameof(CharacterTabSize), nameof(CharacterTabSize.Expand), ExpandMethod))
        {
            return instructions;
        }

        return InsertExpand(instructions);
    }

    private static IEnumerable<CodeInstruction> InsertExpand(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(PawnCardSizeMethod!))
            {
                yield return new CodeInstruction(OpCodes.Call, ExpandMethod);
            }
        }
    }
}
