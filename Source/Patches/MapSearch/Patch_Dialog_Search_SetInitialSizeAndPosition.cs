using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_Search<Thing>), "SetInitialSizeAndPosition")]
public static class Patch_Dialog_Search_SetInitialSizeAndPosition
{
    public static void Postfix(Window __instance)
    {
        MapSearchUi.AfterSetSize(__instance);
    }
}
