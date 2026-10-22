using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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

        Pawn? pawn = CharacterTabSize.SelectedPawn();
        Size(__instance) = CharacterTabSize.Expand(Size(__instance), pawn);
    }
}

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

// Keeps the left column's section list so the postfix can measure it after other mods add theirs.
[HarmonyPatch(typeof(CharacterCardUtility), "DoLeftSection")]
public static class Patch_CharacterCardUtility_DoLeftSection
{
    private static readonly MethodInfo RememberMethod =
        AccessTools.Method(typeof(CharacterTabSize), nameof(CharacterTabSize.RememberSections));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int index = codes.FindIndex(code =>
            code.opcode == OpCodes.Newobj
            && code.operand is ConstructorInfo ctor
            && ctor.DeclaringType is { IsGenericType: true } type
            && type.GetGenericTypeDefinition() == typeof(List<>)
            && type.GetGenericArguments()[0].Name == "LeftRectSection");

        if (index < 0 || !CharacterTabSize.CanMeasure)
        {
            Log.Warning("[Vanilla UI+] Could not find the Bio tab's section list. RimWorld may have changed. "
                + "The Bio tab keeps its vanilla size; the rest of the mod is unaffected.");
            return codes;
        }

        codes.InsertRange(index + 1, new[]
        {
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Call, RememberMethod),
        });
        return codes;
    }

    public static void Postfix(Rect leftRect, Pawn pawn)
    {
        CharacterTabSize.Measure(leftRect, pawn);
    }
}
