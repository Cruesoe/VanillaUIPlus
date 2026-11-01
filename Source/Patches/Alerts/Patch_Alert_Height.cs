using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Alert), nameof(Alert.Height), MethodType.Getter)]
public static class Patch_Alert_Height
{
    public static bool Prefix(Alert __instance, ref float __result)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.HeightFor(__instance);
        return false;
    }
}
