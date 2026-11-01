using System.Collections.Generic;
using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_WorldSearch), "InitializeSearchSet")]
public static class Patch_Dialog_WorldSearch_InitializeSearchSet
{
    public static void Postfix(List<WorldSearchElement> ___searchSet)
    {
        if (UiPlusMod.Settings.worldSearchAllTiles)
        {
            WorldSearchTiles.AddLandTiles(___searchSet);
        }
    }
}
