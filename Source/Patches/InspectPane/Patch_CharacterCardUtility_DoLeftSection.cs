using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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
