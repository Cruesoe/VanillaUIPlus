using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Vanilla only searches while text is typed; an advanced search runs without it.
[HarmonyPatch(typeof(Dialog_Search<Thing>), "Searching", MethodType.Getter)]
public static class Patch_Dialog_Search_Searching
{
    public static void Postfix(Window __instance, ref bool __result)
    {
        if (__result || __instance is not Dialog_WorldSearch world || !WorldQueryPanel.Active || !SearchDialogAccess<WorldSearchElement>.Ready)
        {
            return;
        }

        List<WorldSearchElement> all = SearchDialogAccess<WorldSearchElement>.AllElements!(world);
        __result = all.Count > 0 && SearchDialogAccess<WorldSearchElement>.SearchIndex!(world) < all.Count;
    }
}
