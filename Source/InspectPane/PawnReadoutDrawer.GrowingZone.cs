using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The body of the base game's pane for a growing zone: two rows of cards, rows for the oldest plant and blight, and the season now.
/// </summary>
public static partial class PawnReadoutDrawer
{
    internal static readonly Texture2D GrowthTex = ContentFinder<Texture2D>.Get("UI/Commands/SetPlantToGrow");
    internal static readonly Texture2D HarvestTex = ContentFinder<Texture2D>.Get("UI/Designators/Harvest");
    internal static readonly Texture2D FertilityTex = ContentFinder<Texture2D>.Get("UI/Buttons/ShowFertilityOverlay");
    internal static readonly Texture2D SeasonTex = ContentFinder<Texture2D>.Get("UI/Icons/DrugPolicy/Schedule");

    internal static Color MutedColor => LabelColor;
    internal static Color GoodColor => TemperatureGreen;
    internal static Color WarningTextColor => WarningColor;

    public static float GrowingZoneBodyHeight(GrowingZoneReadout.Snapshot snapshot)
    {
        int cardRows = (snapshot.Cards.Count + CardsPerRow - 1) / CardsPerRow;
        return PawnReadout.SectionGap + cardRows * PawnReadout.ArmorHeight + snapshot.Rows.Count * PawnReadout.RowHeight
            + PawnReadout.SectionGap + PawnReadout.FooterHeight;
    }

    // Replaces the base game's inspect text, which is all a zone's pane has under its name.
    public static void DrawGrowingZoneBody(Zone_Growing zone, Rect rect)
    {
        GrowingZoneReadout.Snapshot snapshot = GrowingZoneReadout.For(zone);
        try
        {
            float y = rect.y + PawnReadout.SectionGap;
            for (int i = 0; i < snapshot.Cards.Count; i++)
            {
                GrowingZoneReadout.Card card = snapshot.Cards[i];
                Rect slot = CardSlot(rect, y + i / CardsPerRow * PawnReadout.ArmorHeight, i % CardsPerRow);
                CardFace(slot, -1, card.Caption, card.Value, card.ValueColor, card.IconThing, card.IconTexture);
                if (Mouse.IsOver(slot))
                {
                    TooltipHandler.TipRegion(slot, new TipSignal(card.Tip, 0x5C1B01 + i));
                }

                if (card.Click != null && Widgets.ButtonInvisible(slot))
                {
                    card.Click();
                }
            }

            y += (snapshot.Cards.Count + CardsPerRow - 1) / CardsPerRow * PawnReadout.ArmorHeight;
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
                TooltipHandler.TipRegion(footer, new TipSignal(zone.GetInspectString, 0x5C1B0F));
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
}
