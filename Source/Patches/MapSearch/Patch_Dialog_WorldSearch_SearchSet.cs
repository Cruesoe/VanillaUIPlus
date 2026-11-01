using System.Collections.Generic;
using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_WorldSearch), "SearchSet", MethodType.Getter)]
public static class Patch_Dialog_WorldSearch_SearchSet
{
    public static void Postfix(ref List<WorldSearchElement> __result)
    {
        if (WorldSearchRange.ShouldLimit)
        {
            __result = WorldSearchRange.Filter(__result);
        }
    }
}
