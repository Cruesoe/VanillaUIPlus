using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Adds butchering and shearing yields and the revenge chance to a wild animal's inspect text.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class Patch_Pawn_GetInspectString_WildYields
{
    public static void Postfix(Pawn __instance, ref string __result)
    {
        if (!UiPlusMod.Settings.showWildAnimalYields || __instance.Dead || !__instance.RaceProps.Animal || __instance.Faction != null)
        {
            return;
        }

        string yields = WildAnimalYields.Cached(__instance);
        if (yields.Length > 0)
        {
            __result = __result.NullOrEmpty() ? yields : __result.TrimEndNewlines() + "\n" + yields;
        }
    }
}
