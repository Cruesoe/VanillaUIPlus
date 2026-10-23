using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Sets the tab's window size each draw and points FillTab's layout at our own size, so the setting applies immediately.
// FillTab lays out from the static readonly WinSize, which .NET 10 folds into a constant, so writing that field does nothing on 1.7.
[HarmonyPatch(typeof(ITab_Storage), "FillTab")]
public static class Patch_ITab_Storage_FillTab
{
    private static readonly FieldInfo? SizeField = AccessTools.Field(typeof(ITab), "size");
    private static readonly FieldInfo? WinSizeField = AccessTools.Field(typeof(ITab_Storage), "WinSize");

    private static readonly AccessTools.FieldRef<ITab, Vector2>? Size =
        ReflectionGuard.FieldRef<ITab, Vector2>(nameof(ITab_Storage), "size", SizeField);

    // Vanilla's size, restored when the setting is off.
    private static readonly Vector2 VanillaWinSize = WinSizeField?.GetValue(null) is Vector2 winSize ? winSize : new Vector2(300f, 480f);

    // Read by the transpiled FillTab in place of WinSize.
    public static Vector2 CurrentWinSize = VanillaWinSize;

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        if (!ReflectionGuard.Found(nameof(ITab_Storage), "WinSize", WinSizeField))
        {
            return codes;
        }

        FieldInfo replacement = AccessTools.Field(typeof(Patch_ITab_Storage_FillTab), nameof(CurrentWinSize));
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if ((code.opcode == OpCodes.Ldsfld || code.opcode == OpCodes.Ldsflda) && Equals(code.operand, WinSizeField))
            {
                code.operand = replacement;
                replaced++;
            }
        }

        if (replaced == 0)
        {
            ReflectionGuard.Found(nameof(ITab_Storage) + ".FillTab", "WinSize read", null);
        }

        return codes;
    }

    public static void Prefix(ITab_Storage __instance)
    {
        StorageTabSelection.OnStorageTabDrawn(__instance);
        CurrentWinSize = UiPlusMod.Settings.resizeFilterTab
            ? new Vector2(UiPlusMod.Settings.filterTabWidth, UiPlusMod.Settings.filterTabHeight)
            : VanillaWinSize;

        if (Size != null)
        {
            Size(__instance) = CurrentWinSize;
        }
    }

    public static void Postfix(ITab_Storage __instance)
    {
        StorageTabSelection.AfterStorageTabDrawn(__instance);
    }
}

// Reopening the tab counts as a new stockpile for search focus.
[HarmonyPatch(typeof(ITab_Storage), nameof(ITab_Storage.OnOpen))]
public static class Patch_ITab_Storage_OnOpen
{
    public static void Postfix()
    {
        StorageTabSelection.Forget();
    }
}

// Collapses again when a def reload rebuilds the tree, then reopens the saved readout categories.
[HarmonyPatch(typeof(ThingCategoryNodeDatabase), nameof(ThingCategoryNodeDatabase.FinalizeInit))]
public static class Patch_ThingCategoryNodeDatabase_FinalizeInit
{
    public static void Postfix()
    {
        StorageTabSelection.CollapseAll();
        ResourceReadoutTweaks.RestoreOpenCategories();
    }
}
