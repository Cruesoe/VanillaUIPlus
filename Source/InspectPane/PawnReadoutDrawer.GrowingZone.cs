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
        return PawnReadout.SectionGap + CardRowsHeight(snapshot.Cards) + snapshot.Rows.Count * PawnReadout.RowHeight;
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

    internal static float CardRowsHeight(List<ReadoutCard> cards)
    {
        return LayoutCards(cards) * PawnReadout.ArmorHeight;
    }

    private static readonly List<int> CardCells = new List<int>();
    private static readonly List<bool> CellTaken = new List<bool>();

    // Places each card in the first free cell, three to a row, with two-row cards taking the cell below as well; fills CardCells and returns the row count.
    private static int LayoutCards(List<ReadoutCard> cards)
    {
        CardCells.Clear();
        CellTaken.Clear();
        int rows = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            int span = cards[i].Rows > 1 ? 2 : 1;
            int cell = 0;
            while (Taken(cell) || (span > 1 && Taken(cell + CardsPerRow)))
            {
                cell++;
            }

            for (int s = 0; s < span; s++)
            {
                int index = cell + s * CardsPerRow;
                while (CellTaken.Count <= index)
                {
                    CellTaken.Add(false);
                }

                CellTaken[index] = true;
                rows = Mathf.Max(rows, index / CardsPerRow + 1);
            }

            CardCells.Add(cell);
        }

        return rows;
    }

    private static bool Taken(int cell)
    {
        return cell < CellTaken.Count && CellTaken[cell];
    }

    // Cards three to a row from y, with their tooltips and clicks; returns the y under them.
    internal static float DrawCards(Rect rect, float y, List<ReadoutCard> cards, int tipIdBase)
    {
        int rows = LayoutCards(cards);
        for (int i = 0; i < cards.Count; i++)
        {
            ReadoutCard card = cards[i];
            int cell = CardCells[i];
            Rect slot = CardSlot(rect, y + cell / CardsPerRow * PawnReadout.ArmorHeight, cell % CardsPerRow);
            if (card.Rows > 1)
            {
                slot.height += PawnReadout.ArmorHeight;
            }

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

        return y + rows * PawnReadout.ArmorHeight;
    }
}
