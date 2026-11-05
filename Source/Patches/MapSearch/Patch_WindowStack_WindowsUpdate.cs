using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Routes every window's WindowUpdate call through SearchDialogHooks, which adds the search dialogs' per-frame work.
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowsUpdate))]
public static class Patch_WindowStack_WindowsUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        MethodInfo? target = AccessTools.Method(typeof(Window), nameof(Window.WindowUpdate));
        if (!ReflectionGuard.Found(nameof(Window), nameof(Window.WindowUpdate), target))
        {
            return codes;
        }

        MethodInfo replacement = AccessTools.Method(typeof(SearchDialogHooks), nameof(SearchDialogHooks.WindowUpdate));
        bool found = false;
        foreach (CodeInstruction code in codes)
        {
            if (code.Calls(target!))
            {
                code.opcode = OpCodes.Call;
                code.operand = replacement;
                found = true;
            }
        }

        if (!found)
        {
            ReflectionGuard.Found(nameof(WindowStack) + ".WindowsUpdate", "WindowUpdate call", null);
        }

        return codes;
    }
}
