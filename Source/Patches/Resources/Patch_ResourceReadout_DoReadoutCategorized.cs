using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Draws the top level via DrawTopLevel before the listing ends and gives vanilla's loop an empty list, keeping Dubs Mint Menus' pinned section.
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
