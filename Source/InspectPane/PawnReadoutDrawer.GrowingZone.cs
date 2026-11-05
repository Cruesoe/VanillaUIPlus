using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The body of the base game's pane for a growing zone: crop, growth and ready cards, rows for the growing period, oldest plant, blight and fertility, and the season now.
/// </summary>
public static partial class PawnReadoutDrawer
{
    private static readonly Texture2D GrowthTex = ContentFinder<Texture2D>.Get("UI/Commands/SetPlantToGrow");
    private static readonly Texture2D HarvestTex = ContentFinder<Texture2D>.Get("UI/Designators/Harvest");

    public static float GrowingZoneBodyHeight(GrowingZoneReadout.Snapshot snapshot)
    {
        return PawnReadout.SectionGap + PawnReadout.ArmorHeight + snapshot.Rows.Count * PawnReadout.RowHeight
            + PawnReadout.SectionGap + PawnReadout.FooterHeight;
    }

    // Replaces the base game's inspect text, which is all a zone's pane has under its name.
    public static void DrawGrowingZoneBody(Zone_Growing zone, Rect rect)
    {
        GrowingZoneReadout.Snapshot snapshot = GrowingZoneReadout.For(zone);
        try
        {
            float y = rect.y + PawnReadout.SectionGap;
            Rect crop = CardSlot(rect, y, 0);
            CardFace(crop, -1, snapshot.CropLabel, snapshot.PlantedText, Color.white, snapshot.Crop);
            ZoneTip(crop, snapshot, 0);
            if (Widgets.ButtonInvisible(crop))
            {
                Find.WindowStack.Add(new Dialog_InfoCard(snapshot.Crop));
            }

            Rect growth = CardSlot(rect, y, 1);
            CardFace(growth, -1, TextCache.Key("VUIP.ZonePaneGrowthShort"), snapshot.GrowthText, snapshot.Plants > 0 ? Color.white : LabelColor, iconTexture: GrowthTex);
            ZoneTip(growth, snapshot, 1);

            Rect ready = CardSlot(rect, y, 2);
            CardFace(ready, -1, TextCache.Key("VUIP.ZonePaneReadyShort"), snapshot.ReadyText,
                !snapshot.Harvestable ? LabelColor : snapshot.Ready > 0 ? TemperatureGreen : Color.white, iconTexture: HarvestTex);
            ZoneTip(ready, snapshot, 2);

            y += PawnReadout.ArmorHeight;
            for (int i = 0; i < snapshot.Rows.Count; i++)
            {
                HealthRow(rect, ref y, snapshot.Rows[i], 0x5C1B10 + i);
            }

            Rect footer = new Rect(rect.x, y + PawnReadout.SectionGap, rect.width, PawnReadout.FooterHeight);
            GUI.color = DividerColor;
            Widgets.DrawLineHorizontal(rect.x, footer.y - PawnReadout.SectionGap / 2f, rect.width);
            GUI.color = Color.white;
            Widgets.DrawHighlightIfMouseover(footer);
            Label(new Rect(footer.x + 4f, footer.y, footer.width - 6f, footer.height), snapshot.StatusText,
                snapshot.GrowingNow ? Color.white : ColorLibrary.RedReadable);
            if (Mouse.IsOver(footer))
            {
                TooltipHandler.TipRegion(footer, new TipSignal(zone.GetInspectString, 0x5C1B04));
            }
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] Error drawing growing zone readout for {zone}: {exception}", 0x5C1B7E);
        }
        finally
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }
    }

    // Takes the snapshot's tooltip method by index so its delegate is only allocated while the card is hovered.
    private static void ZoneTip(Rect rect, GrowingZoneReadout.Snapshot snapshot, int card)
    {
        if (!Mouse.IsOver(rect))
        {
            return;
        }

        Func<string> tip = card switch
        {
            0 => snapshot.PlantedTip,
            1 => snapshot.GrowthTip,
            _ => snapshot.ReadyTip,
        };
        TooltipHandler.TipRegion(rect, new TipSignal(tip, 0x5C1B01 + card));
    }
}
