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
            // Gives back the base game's width after the improved tab widened it; another mod's own width is left alone.
            if (GearTabAccess.Available && GearTabAccess.Size(__instance).x == GearTabDrawer.TabWidth)
            {
                GearTabAccess.Size(__instance).x = GearTabDrawer.VanillaWidth;
            }

            return true;
        }

        GearTabDrawer.Draw(__instance);
        return false;
    }
}
