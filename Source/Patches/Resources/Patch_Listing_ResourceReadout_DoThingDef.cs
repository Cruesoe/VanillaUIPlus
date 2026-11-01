using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

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
