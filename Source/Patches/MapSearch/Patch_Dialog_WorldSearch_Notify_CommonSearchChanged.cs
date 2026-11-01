using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_WorldSearch), nameof(Dialog_WorldSearch.Notify_CommonSearchChanged))]
public static class Patch_Dialog_WorldSearch_Notify_CommonSearchChanged
{
    public static void Postfix()
    {
        WorldSearchHighlight.NotifySearchRestarted();
    }
}
