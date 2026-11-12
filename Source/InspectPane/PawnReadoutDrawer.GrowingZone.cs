using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The body of the base game's pane for a growing zone: two rows of cards and a row for blight.
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
        return PawnReadout.SectionGap + CardRowsHeight(snapshot.Cards.Count) + snapshot.Rows.Count * PawnReadout.RowHeight;
    }

    // Replaces the base game's inspect text, which is all a zone's pane has under its name.
    public static void DrawGrowingZoneBody(Zone_Growing zone, Rect rect)
    {
        GrowingZoneReadout.Snapshot snapshot = GrowingZoneReadout.For(zone);
        try
        {
            float y = DrawCards(rect, rect.y + PawnReadout.SectionGap, snapshot.Cards, 0x5C1B01);
            for (int i = 0; i < snapshot.Rows.Count; i++)
            {
                HealthRow(rect, ref y, snapshot.Rows[i], 0x5C1B10 + i);
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

    internal static float CardRowsHeight(int cards)
    {
        return (cards + CardsPerRow - 1) / CardsPerRow * PawnReadout.ArmorHeight;
    }

    // Cards three to a row from y, with their tooltips and clicks; returns the y under them.
    internal static float DrawCards(Rect rect, float y, List<ReadoutCard> cards, int tipIdBase)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            ReadoutCard card = cards[i];
            Rect slot = CardSlot(rect, y + i / CardsPerRow * PawnReadout.ArmorHeight, i % CardsPerRow);
            CardFace(slot, card.Icon, card.Caption, card.Value, card.ValueColor, card.IconThing, card.IconTexture, card.IconInstance);
            if (Mouse.IsOver(slot))
            {
                TooltipHandler.TipRegion(slot, new TipSignal(card.TipText, tipIdBase + i));
            }

            if (card.Click != null && Widgets.ButtonInvisible(slot))
            {
                card.Click();
            }
        }

        return y + CardRowsHeight(cards.Count);
    }
}
