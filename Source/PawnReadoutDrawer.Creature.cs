using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The body of the base game's pane for animals, insects, mechs and other creatures without the colonist readout: cards for armor, durability, yields, revenge chance and combat, then the remaining inspect text.
/// </summary>
public static partial class PawnReadoutDrawer
{
    private const int CardsPerRow = 3;
    private const float CreatureRefreshSeconds = 0.25f;
    // Where the base game's pane starts its text (health bar row).
    private const float CreatureTop = 21f;
    private const float PaneChromeHeight = 40f;

    private static CreatureView? creatureView;
    private static float creatureBodyWidth = 600f;

    private sealed class CreatureCard
    {
        public string Caption = string.Empty;
        public string Value = string.Empty;
        public Color ValueColor = Color.white;
        public int Icon = -1;
        public ThingDef? IconThing;
        public Texture2D? IconTexture;
        public Func<PawnReadout.Snapshot, string> Tip = _ => string.Empty;
        public int TipId;
    }

    private sealed class CreatureView
    {
        public Pawn Pawn = null!;
        public float RealTime;
        public PawnReadout.Snapshot Snapshot = null!;
        public readonly List<CreatureCard> Cards = new List<CreatureCard>();
        public string Text = string.Empty;
        public float TextHeight;

        public int Rows => (Cards.Count + CardsPerRow - 1) / CardsPerRow;
    }

    /// <summary>Pane height for a creature with the card rows and the inspect text that is left.</summary>
    public static float CreaturePaneHeight(Pawn pawn)
    {
        CreatureView view = CreatureViewFor(pawn);
        float body = PawnReadout.SectionGap + view.Rows * PawnReadout.ArmorHeight + view.TextHeight + PawnReadout.SectionGap;
        return Mathf.Max(InspectPaneUtility.PaneHeight, CreatureTop + body + PaneChromeHeight);
    }

    // Replaces the base game's inspect text area (under its bar row): the cards, then the text the cards don't cover.
    public static void DrawCreatureBody(Pawn pawn, Rect rect)
    {
        CreatureView view = CreatureViewFor(pawn);
        creatureBodyWidth = rect.width;
        float y = rect.y + PawnReadout.SectionGap;
        for (int i = 0; i < view.Cards.Count; i++)
        {
            CreatureCard card = view.Cards[i];
            Rect slot = CardSlot(rect, y + i / CardsPerRow * PawnReadout.ArmorHeight, i % CardsPerRow);
            Card(slot, card.Icon, card.Caption, card.Value, card.ValueColor, view.Snapshot, card.Tip, card.TipId, card.IconThing, card.IconTexture);
        }

        float textY = y + view.Rows * PawnReadout.ArmorHeight;
        if (view.Text.Length > 0)
        {
            InspectPaneFiller.DrawInspectString(view.Text, new Rect(rect.x, textY, rect.width, Mathf.Max(0f, rect.yMax - textY)));
        }
    }

    private static CreatureView CreatureViewFor(Pawn pawn)
    {
        float now = Time.realtimeSinceStartup;
        CreatureView? current = creatureView;
        if (current != null && current.Pawn == pawn && now - current.RealTime < CreatureRefreshSeconds && now >= current.RealTime)
        {
            return current;
        }

        current = new CreatureView { Pawn = pawn, RealTime = now, Snapshot = PawnReadout.For(pawn) };
        AddCreatureCards(current, pawn);
        current.Text = CreatureText(pawn, current.Snapshot);
        if (current.Text.Length > 0)
        {
            GameFont font = Text.Font;
            Text.Font = GameFont.Small;
            current.TextHeight = Text.CalcHeight(current.Text, creatureBodyWidth);
            Text.Font = font;
        }

        creatureView = current;
        return current;
    }

    private static void AddCreatureCards(CreatureView view, Pawn pawn)
    {
        PawnReadout.Snapshot snapshot = view.Snapshot;
        UiPlusSettings settings = UiPlusMod.Settings;
        view.Cards.Add(new CreatureCard { Icon = 0, Caption = TextCache.Key("VUIP.PawnPaneArmorSharpShort"), Value = snapshot.ArmorSharpText, Tip = ArmorTip, TipId = 0x5C1A21 });
        view.Cards.Add(new CreatureCard { Icon = 1, Caption = TextCache.Key("VUIP.PawnPaneArmorBluntShort"), Value = snapshot.ArmorBluntText, Tip = ArmorTip, TipId = 0x5C1A22 });
        view.Cards.Add(new CreatureCard { Icon = 2, Caption = TextCache.Key("VUIP.PawnPaneArmorHeatShort"), Value = snapshot.ArmorHeatText, Tip = ArmorTip, TipId = 0x5C1A23 });
        if (snapshot.HasDurability)
        {
            Color color = snapshot.DurabilityPercent < 0.25f ? ColorLibrary.RedReadable : snapshot.DurabilityPercent < 0.5f ? WarningColor : Color.white;
            view.Cards.Add(new CreatureCard
            {
                IconTexture = ArmorDurabilityTex, Value = snapshot.DurabilityText, ValueColor = color, Tip = DurabilityTip, TipId = 0x5C1A24
            });
        }

        if (WildAnimalYields.ShowsCards(pawn))
        {
            AddYieldCard(view, pawn, pawn.RaceProps.meatDef, StatDefOf.MeatAmount, 0x5C1A25);
            AddYieldCard(view, pawn, pawn.RaceProps.leatherDef, StatDefOf.LeatherAmount, 0x5C1A26);
            string revenge = PawnUtility.GetManhunterOnDamageChance(pawn).ToStringPercent();
            view.Cards.Add(new CreatureCard
            {
                Icon = 9, Caption = TextCache.Key("VUIP.PawnPaneRevengeShort"), Value = revenge, TipId = 0x5C1A27,
                Tip = _ => "HarmedRevengeChance".Translate().Resolve().AsTipTitle() + "\n\n" + "HarmedRevengeChanceExplanation".Translate()
            });
        }

        if (settings.pawnPaneShowSpeed)
        {
            view.Cards.Add(new CreatureCard
            {
                Icon = 4, Caption = TextCache.Key("VUIP.PawnPaneMovementShort"), Value = snapshot.MoveText, TipId = 0x5C1A03,
                Tip = s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed)
            });
        }

        if (settings.pawnPaneShowDps)
        {
            view.Cards.Add(new CreatureCard
            {
                Icon = 6, Caption = TextCache.Key(snapshot.Ranged ? "VUIP.PawnPaneRangedDpsShort" : "VUIP.PawnPaneMeleeDpsShort"), Value = snapshot.DpsText,
                ValueColor = snapshot.Ranged && snapshot.Dps < 0f ? LabelColor : Color.white, Tip = CombatTip, TipId = 0x5C1A04
            });
        }
    }

    // Meat or leather: the product's icon and name, with the amount a butcher gets before efficiency.
    private static void AddYieldCard(CreatureView view, Pawn pawn, ThingDef? product, StatDef stat, int tipId)
    {
        if (product == null)
        {
            return;
        }

        float amount = pawn.GetStatValue(stat);
        if (Mathf.RoundToInt(amount) <= 0)
        {
            return;
        }

        view.Cards.Add(new CreatureCard
        {
            IconThing = product, Caption = product.LabelCap, Value = Mathf.RoundToInt(amount).ToString(), TipId = tipId,
            Tip = s => StatTip(s.Pawn, stat, amount) + "\n\n" + "VUIP.PawnPaneYieldTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor)
        });
    }

    // The base game's inspect text without the lines the header icons and cards already show.
    private static string CreatureText(Pawn pawn, PawnReadout.Snapshot snapshot)
    {
        string[] lines = InspectText(pawn).Split('\n');
        string basics = pawn.MainDesc(false);
        string durability = PawnReadout.CombatExtendedActive ? "CE_ArmorDurability".Translate().ToString().Trim() : string.Empty;
        List<string> kept = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (i == 0 && line == basics || snapshot.HasDurability && durability.Length > 0 && line.StartsWith(durability))
            {
                continue;
            }

            kept.Add(line);
        }

        return string.Join("\n", kept).Trim('\n');
    }

    /// <summary>Drops the cached view so an unloaded game isn't kept in memory.</summary>
    public static void ForgetCreature()
    {
        creatureView = null;
    }
}
