using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Edits the categorized rows in place (Dubs Mint Menus transpiles them too): the count call, the zero-count check and a row-end hook; all or nothing.
internal static class ResourceRowTranspiler
{
    private static readonly MethodInfo? EndLine = AccessTools.Method(typeof(Listing_Lines), "EndLine");

    public static IEnumerable<CodeInstruction> Apply(
        IEnumerable<CodeInstruction> instructions, string owner, MethodInfo? countCall, MethodInfo countReplacement,
        MethodInfo showCheck, MethodInfo rowHook, params OpCode[] hookArgs)
    {
        List<CodeInstruction> original = instructions.ToList();
        if (countCall == null || EndLine == null)
        {
            ReflectionGuard.Found(owner, "resource count call", null);
            return original;
        }

        // Every spot is found before any edit; edits are in place so branch labels survive.
        int countIndex = original.FindIndex(i => i.Calls(countCall));
        int branchIndex = countIndex < 0 ? -1 : original.FindIndex(countIndex + 1, i => i.Branches(out _));
        int endLineIndex = original.FindIndex(i => i.Calls(EndLine));
        if (countIndex < 0 || branchIndex < 0 || endLineIndex < 0 || endLineIndex < branchIndex)
        {
            ReflectionGuard.Found(owner, "row layout", null);
            return original;
        }

        original[countIndex].opcode = OpCodes.Call;
        original[countIndex].operand = countReplacement;

        // The hook loads its own arguments and takes over EndLine's labels.
        List<CodeInstruction> hook = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_0) };
        foreach (OpCode arg in hookArgs)
        {
            hook.Add(new CodeInstruction(arg));
        }

        hook.Add(new CodeInstruction(OpCodes.Call, rowHook));
        hook[0].MoveLabelsFrom(original[endLineIndex]);
        original.InsertRange(endLineIndex, hook);

        // The count is on the stack at the branch; the check turns it into "draw or not".
        original.InsertRange(branchIndex, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(original[branchIndex]),
            new CodeInstruction(OpCodes.Call, showCheck),
        });

        return original;
    }
}
