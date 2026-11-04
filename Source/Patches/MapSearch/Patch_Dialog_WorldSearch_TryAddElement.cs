using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Vanilla resizes after each added result through the shared Dialog_Search code, which this mod can't patch on 1.7.
[HarmonyPatch(typeof(Dialog_WorldSearch), "TryAddElement")]
public static class Patch_Dialog_WorldSearch_TryAddElement
{
    public static void Postfix(Window __instance)
    {
        MapSearchUi.AfterSetSize(__instance);
    }
}
