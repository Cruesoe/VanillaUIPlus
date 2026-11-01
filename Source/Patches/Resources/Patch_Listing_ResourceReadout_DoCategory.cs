using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

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
