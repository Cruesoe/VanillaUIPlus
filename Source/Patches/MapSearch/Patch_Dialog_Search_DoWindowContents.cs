using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Dialog_Search<T> code is shared between reference types, so these Dialog_Search<Thing> patches also run for the world search.
[HarmonyPatch(typeof(Dialog_Search<Thing>), nameof(Dialog_Search<Thing>.DoWindowContents))]
public static class Patch_Dialog_Search_DoWindowContents
{
    public static bool Prefix(Window __instance, Rect inRect)
    {
        if (__instance is Dialog_MapSearch map && MapSearchUi.MapReady)
        {
            MapSearchUi.DrawMap(map, inRect);
            return false;
        }

        if (__instance is Dialog_WorldSearch world && MapSearchUi.WorldReady)
        {
            MapSearchUi.DrawWorld(world, inRect);
            return false;
        }

        return true;
    }
}
