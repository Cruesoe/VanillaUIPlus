using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_MapSearch), "SetInitialSizeAndPosition")]
public static class Patch_Dialog_MapSearch_SetInitialSizeAndPosition
{
    public static void Postfix(Window __instance)
    {
        MapSearchUi.AfterSetSize(__instance);
    }
}
