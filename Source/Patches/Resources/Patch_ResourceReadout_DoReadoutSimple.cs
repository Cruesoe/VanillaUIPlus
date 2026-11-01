using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Redraws the simple list in the saved order with vanilla's layout.
[HarmonyPatch(typeof(ResourceReadout), "DoReadoutSimple")]
public static class Patch_ResourceReadout_DoReadoutSimple
{
    private static readonly AccessTools.FieldRef<ResourceReadout, Vector2>? ScrollPosition =
        ReflectionGuard.FieldRef<ResourceReadout, Vector2>(nameof(ResourceReadout), "scrollPosition",
            AccessTools.Field(typeof(ResourceReadout), "scrollPosition"));
    private static readonly AccessTools.FieldRef<ResourceReadout, float>? LastDrawnHeight =
        ReflectionGuard.FieldRef<ResourceReadout, float>(nameof(ResourceReadout), "lastDrawnHeight",
            AccessTools.Field(typeof(ResourceReadout), "lastDrawnHeight"));

    public static bool Prefix(ResourceReadout __instance, Rect rect, float outRectHeight)
    {
        if (ScrollPosition == null || LastDrawnHeight == null)
        {
            return true;
        }

        ResourceCounter counter = Find.CurrentMap.resourceCounter;
        Vector2 scroll = ScrollPosition(__instance);
        List<Def> all = ResourceReadoutTweaks.Simple(counter.AllCountedAmounts);

        Widgets.BeginGroup(rect);
        Text.Anchor = TextAnchor.MiddleLeft;
        ResourceReadoutTweaks.BeginSimple(all);
        float y = 0f;
        try
        {
            foreach (Def def in all)
            {
                ThingDef thing = (ThingDef)def;
                int count = ResourceReadoutTweaks.Count(counter, thing);
                if (!ResourceReadoutTweaks.ShowSimple(count, thing))
                {
                    continue;
                }

                Rect row = new Rect(0f, y, rect.width, ResourceReadoutTweaks.SimpleRowHeight);
                if (row.yMax >= scroll.y && row.y <= scroll.y + outRectHeight)
                {
                    DrawRow(row, thing, count);
                }

                ResourceReadoutTweaks.SimpleRow(row, thing);
                y += ResourceReadoutTweaks.SimpleRowHeight;
            }
        }
        finally
        {
            ResourceReadoutTweaks.EndSimple();
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndGroup();
        }

        LastDrawnHeight(__instance) = y;
        return false;
    }

    // Vanilla's DrawResourceSimple, with the count passed in.
    private static void DrawRow(Rect row, ThingDef def, int count)
    {
        Rect icon = new Rect(row.x, row.y, 27f, 27f);
        Color color = GUI.color;
        Widgets.ThingIcon(icon, def);
        GUI.color = color;
        if (Mouse.IsOver(icon))
        {
            TooltipHandler.TipRegion(icon, def.LabelCap + ": " + def.description.CapitalizeFirst());
        }

        Widgets.Label(new Rect(34f, row.y + 2f, row.width - 34f, row.height), count.ToStringCached());
    }
}
