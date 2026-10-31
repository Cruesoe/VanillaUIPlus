using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Adds a recreation building's recreation type to its inspect text, as its info card lists it.
[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
public static class Patch_ThingWithComps_GetInspectString_RecreationType
{
    public static void Postfix(ThingWithComps __instance, ref string __result)
    {
        JoyKindDef? joyKind = __instance.def.building?.joyKind;
        if (joyKind == null || !UiPlusMod.Settings.showRecreationType)
        {
            return;
        }

        string line = "StatsReport_JoyKind".Translate() + ": " + joyKind.LabelCap;
        __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
    }
}
