using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Combat Extended's Gear tab overrides FillTab without calling the base game's, so it gets the same prefix.
[HarmonyPatch]
public static class Patch_CombatExtended_ITab_Inventory_FillTab
{
    public static bool Prepare()
    {
        return PawnReadout.CombatExtendedActive && TargetMethod() != null;
    }

    public static MethodBase? TargetMethod()
    {
        return AccessTools.Method("CombatExtended.ITab_Inventory:FillTab");
    }

    public static bool Prefix(ITab_Pawn_Gear __instance)
    {
        return Patch_ITab_Pawn_Gear_FillTab.Prefix(__instance);
    }
}
