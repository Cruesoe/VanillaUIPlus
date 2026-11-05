using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Draws the improved Gear tab in place of the base game's when it's turned on.
[HarmonyPatch(typeof(ITab_Pawn_Gear), "FillTab")]
public static class Patch_ITab_Pawn_Gear_FillTab
{
    public static bool Prefix(ITab_Pawn_Gear __instance)
    {
        if (!GearTab.Enabled)
        {
            return true;
        }

        GearTabDrawer.Draw(__instance);
        return false;
    }
}
