using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Routes every window's DoWindowContents and QuickSearchWidgetRect calls through SearchDialogHooks, which handles the search dialogs.
[HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
public static class Patch_Window_InnerWindowOnGUI
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        Redirect(codes, AccessTools.Method(typeof(Window), nameof(Window.DoWindowContents)), nameof(SearchDialogHooks.DoWindowContents));
        if (SearchDialogHooks.QuickSearchRect != null)
        {
            Redirect(codes, AccessTools.Method(typeof(Window), "QuickSearchWidgetRect"), nameof(SearchDialogHooks.QuickSearchWidgetRect));
        }

        return codes;
    }

    private static void Redirect(List<CodeInstruction> codes, MethodInfo? target, string hook)
    {
        if (!ReflectionGuard.Found(nameof(Window), hook, target))
        {
            return;
        }

        MethodInfo replacement = AccessTools.Method(typeof(SearchDialogHooks), hook);
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
            ReflectionGuard.Found(nameof(Window) + ".InnerWindowOnGUI", hook + " call", null);
        }
    }
}
