using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Gear and genes tabs cap their height with the base game's 165px pane; use the current pane height instead.
[HarmonyPatch]
public static class Patch_TabHeightCaps
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ITab_Pawn_Gear), "FillTab");
        yield return AccessTools.Method(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGenesInfo));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo paneHeight = AccessTools.Method(typeof(PawnReadout), nameof(PawnReadout.CurrentPaneHeight));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && instruction.operand is float value && value == InspectPaneUtility.PaneHeight)
            {
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, paneHeight).MoveLabelsFrom(instruction);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}
