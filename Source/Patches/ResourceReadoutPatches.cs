using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// The categorized rows are changed in place rather than redrawn, because Dubs Mint Menus
// transpiles the same two methods for its ctrl-click pinning. Three edits each: the count
// goes through ResourceReadoutTweaks (for count-all-on-map), the "skip if zero" check can
// be overruled (for showing zero counts), and a hook runs just before the row ends (for
// dragging and the right-click menu). If any of the three is not found the method is
// left exactly as vanilla wrote it.
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

        // Every spot is found before anything is touched, so a miss leaves vanilla intact.
        // The instructions are then edited in place: CodeInstruction.Clone drops labels,
        // which would leave the method's own branches pointing nowhere.
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

        // The listing is already on the stack for EndLine; the hook loads its own. Any
        // label on the call moves to the hook so jumps still run it.
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

[HarmonyPatch(typeof(Listing_ResourceReadout), nameof(Listing_ResourceReadout.DoCategory))]
public static class Patch_Listing_ResourceReadout_DoCategory
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ResourceRowTranspiler.Apply(
            instructions,
            nameof(Listing_ResourceReadout) + ".DoCategory",
            AccessTools.Method(typeof(ResourceCounter), nameof(ResourceCounter.GetCountIn), new[] { typeof(ThingCategoryDef) }),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.CountIn)),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.ShowCategory)),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.CategoryRow)),
            OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3);
    }
}

[HarmonyPatch(typeof(Listing_ResourceReadout), "DoThingDef")]
public static class Patch_Listing_ResourceReadout_DoThingDef
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ResourceRowTranspiler.Apply(
            instructions,
            nameof(Listing_ResourceReadout) + ".DoThingDef",
            AccessTools.Method(typeof(ResourceCounter), nameof(ResourceCounter.GetCount), new[] { typeof(ThingDef) }),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.Count)),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.ShowThing)),
            AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.ThingRow)),
            OpCodes.Ldarg_1, OpCodes.Ldarg_2);
    }
}

// Children in the saved order, categories and things mixed, each list part of the joined
// drag set.
[HarmonyPatch(typeof(Listing_ResourceReadout), nameof(Listing_ResourceReadout.DoCategoryChildren))]
public static class Patch_Listing_ResourceReadout_DoCategoryChildren
{
    public static bool Prefix(Listing_ResourceReadout __instance, TreeNode_ThingCategory node, int indentLevel, int openMask)
    {
        if (ResourceReadoutTweaks.DoThingDef == null)
        {
            return true;
        }

        ResourceReadoutTweaks.DrawChildren(__instance, node, indentLevel, openMask);
        return false;
    }
}

// The top level is drawn by ResourceReadoutTweaks.DrawTopLevel, just before the listing
// ends, so things moved there can sit among the categories. Vanilla's own loop is kept
// but given an empty list, which leaves Dubs Mint Menus' pinned section (inserted before
// the loop) where it is. If either spot is missing the method is left as vanilla wrote it.
[HarmonyPatch(typeof(ResourceReadout), "DoReadoutCategorized")]
public static class Patch_ResourceReadout_DoReadoutCategorized
{
    private static readonly FieldInfo? RootsField = AccessTools.Field(typeof(ResourceReadout), "RootThingCategories");
    private static readonly MethodInfo? ListingEnd = AccessTools.Method(typeof(Listing), nameof(Listing.End));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int endIndex = ListingEnd == null ? -1 : codes.FindLastIndex(i => i.Calls(ListingEnd));
        bool hasRoots = RootsField != null && codes.Exists(i => i.LoadsField(RootsField));
        if (endIndex < 0 || !hasRoots)
        {
            ReflectionGuard.Found(nameof(ResourceReadout), "DoReadoutCategorized layout", null);
            return codes;
        }

        // The listing is on the stack for End; a copy goes to the top-level drawer.
        codes.InsertRange(endIndex, new[]
        {
            new CodeInstruction(OpCodes.Dup).MoveLabelsFrom(codes[endIndex]),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.DrawTopLevel))),
        });

        MethodInfo filter = AccessTools.Method(typeof(ResourceReadoutTweaks), nameof(ResourceReadoutTweaks.VanillaTopLevel));
        for (int i = codes.Count - 1; i >= 0; i--)
        {
            if (codes[i].LoadsField(RootsField))
            {
                codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, filter));
            }
        }

        ResourceReadoutTweaks.DrawsTopLevel = true;
        return codes;
    }

    public static void Postfix()
    {
        ResourceReadoutTweaks.EndTopLevel();
    }
}

// The simple list is short and private, so it is redrawn here in the saved order with the
// same layout as vanilla.
[HarmonyPatch(typeof(ResourceReadout), "DoReadoutSimple")]
public static class Patch_ResourceReadout_DoReadoutSimple
{
    private static readonly AccessTools.FieldRef<ResourceReadout, Vector2>? ScrollPosition =
        ReflectionGuard.FieldRef<ResourceReadout, Vector2>(nameof(ResourceReadout), "scrollPosition",
            AccessTools.Field(typeof(ResourceReadout), "scrollPosition"));
    private static readonly AccessTools.FieldRef<ResourceReadout, float>? LastDrawnHeight =
        ReflectionGuard.FieldRef<ResourceReadout, float>(nameof(ResourceReadout), "lastDrawnHeight",
            AccessTools.Field(typeof(ResourceReadout), "lastDrawnHeight"));

    public static bool Prefix(ResourceReadout __instance, Rect rect, float outRectHeight)
    {
        if (ScrollPosition == null || LastDrawnHeight == null)
        {
            return true;
        }

        ResourceCounter counter = Find.CurrentMap.resourceCounter;
        Vector2 scroll = ScrollPosition(__instance);
        List<Def> all = ResourceReadoutTweaks.Simple(counter.AllCountedAmounts);

        Widgets.BeginGroup(rect);
        Text.Anchor = TextAnchor.MiddleLeft;
        ResourceReadoutTweaks.BeginSimple(all);
        float y = 0f;
        try
        {
            foreach (Def def in all)
            {
                ThingDef thing = (ThingDef)def;
                int count = ResourceReadoutTweaks.Count(counter, thing);
                if (!ResourceReadoutTweaks.ShowSimple(count, thing))
                {
                    continue;
                }

                Rect row = new Rect(0f, y, rect.width, ResourceReadoutTweaks.SimpleRowHeight);
                if (row.yMax >= scroll.y && row.y <= scroll.y + outRectHeight)
                {
                    DrawRow(row, thing, count);
                }

                ResourceReadoutTweaks.SimpleRow(row, thing);
                y += ResourceReadoutTweaks.SimpleRowHeight;
            }
        }
        finally
        {
            ResourceReadoutTweaks.EndSimple();
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndGroup();
        }

        LastDrawnHeight(__instance) = y;
        return false;
    }

    // Vanilla's DrawResourceSimple, with the count passed in.
    private static void DrawRow(Rect row, ThingDef def, int count)
    {
        Rect icon = new Rect(row.x, row.y, 27f, 27f);
        Color color = GUI.color;
        Widgets.ThingIcon(icon, def);
        GUI.color = color;
        if (Mouse.IsOver(icon))
        {
            TooltipHandler.TipRegion(icon, def.LabelCap + ": " + def.description.CapitalizeFirst());
        }

        Widgets.Label(new Rect(34f, row.y + 2f, row.width - 34f, row.height), count.ToStringCached());
    }
}

[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
public static class Patch_ResourceCounter_UpdateResourceCounts
{
    public static void Postfix()
    {
        ResourceReadoutTweaks.NotifyCountsUpdated();
    }
}
