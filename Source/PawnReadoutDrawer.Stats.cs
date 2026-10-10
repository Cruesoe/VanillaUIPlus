using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static partial class PawnReadoutDrawer
{
    private const float CardGap = 4f;
    private const float IconSpace = 28f;
    private const float ThermometerMinWidth = 76f;
    private static readonly Color CardBorder = new Color(0.36f, 0.42f, 0.45f, 0.8f);
    private static readonly Color CardBackground = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color ArmorFill = new Color(0.55f, 0.79f, 0.48f);
    private static readonly Color TemperatureGreen = new Color(0.57f, 0.76f, 0.48f);
    private static readonly Color TemperatureAmber = new Color(0.95f, 0.74f, 0.34f);
    private static readonly Color TemperatureRed = new Color(0.9f, 0.37f, 0.43f);
    private static readonly Texture2D ReadoutIcons = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/PawnReadoutIcons");
    private static readonly Rect[] IconPixels =
    {
        new Rect(130f, 111f, 230f, 268f), new Rect(580f, 99f, 212f, 287f),
        new Rect(1015f, 98f, 213f, 291f), new Rect(1464f, 98f, 154f, 283f),
        new Rect(145f, 519f, 221f, 262f), new Rect(557f, 532f, 251f, 249f),
        new Rect(967f, 512f, 279f, 278f)
    };
    private static LoadedLanguage? measuredLanguage;
    private static float measuredScale = -1f;
    private static float statLabelWidth = -1f;
    private static float minStatsWidth = -1f;
    private static GUIStyle? armorValueStyle;
    private static readonly GUIContent MeasureContent = new GUIContent();

    private static void DrawStats(PawnReadout.Snapshot snapshot, Rect rect)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        float y = rect.y;
        if (settings.pawnPaneShowArmor)
        {
            if (PawnReadout.CombatExtendedActive)
            {
                StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneArmor"), "-", LabelColor, snapshot,
                    _ => TextCache.Key("VUIP.PawnPaneArmorCombatExtended"), 0x5C1A01, 1);
            }
            else
            {
                ArmorRow(rect, ref y, snapshot);
            }
        }

        if (settings.pawnPaneShowTemperature)
        {
            ComfortRow(rect, ref y, snapshot);
        }

        if (settings.pawnPaneShowSpeed)
        {
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneMovement"), snapshot.MoveText, Color.white, snapshot,
                s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed), 0x5C1A03, 4);
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneWorkSpeed"), snapshot.WorkText, Color.white, snapshot,
                s => StatTip(s.Pawn, StatDefOf.WorkSpeedGlobal, s.WorkSpeed), 0x5C1A09, 5);
        }

        if (settings.pawnPaneShowDps)
        {
            StatRow(rect, ref y, snapshot.Ranged ? TextCache.Key("VUIP.PawnPaneRangedDps") : TextCache.Key("VUIP.PawnPaneMeleeDps"),
                snapshot.DpsText, snapshot.Ranged && snapshot.Dps < 0f ? LabelColor : Color.white, snapshot, CombatTip, 0x5C1A04, 6);
        }

        if (snapshot.BleedRate > 0f)
        {
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneBleeding"), snapshot.BleedingText, ColorLibrary.RedReadable,
                snapshot, s => s.BleedingText, 0x5C1A06);
        }

        if (snapshot.LowNeeds.Count > 0)
        {
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneLowNeeds"), snapshot.LowNeedsText,
                snapshot.LowNeedsCritical ? ColorLibrary.RedReadable : WarningColor, snapshot, NeedsTip, 0x5C1A07);
        }
    }

    // One row per need: label, a bar coloured by the low-needs threshold, and the level.
    private static void DrawNeeds(PawnReadout.Snapshot snapshot, Rect rect)
    {
        float labelWidth = 0f;
        for (int i = 0; i < snapshot.Needs.Count; i++)
        {
            labelWidth = Mathf.Max(labelWidth, TextCache.Width(snapshot.Needs[i].LabelCap));
        }

        float threshold = UiPlusMod.Settings.pawnPaneNeedThreshold / 100f;
        float valueWidth = TextCache.Width("100%") + 6f;
        float y = rect.y;
        for (int i = 0; i < snapshot.Needs.Count; i++)
        {
            Need need = snapshot.Needs[i];
            float level = snapshot.NeedLevels[i];
            Rect row = NextStatRow(rect, ref y);
            float shownLabel = Mathf.Min(labelWidth + 4f, row.width * 0.45f);
            Label(new Rect(row.x + 4f, row.y, shownLabel, row.height), need.LabelCap, LabelColor);
            Label(new Rect(row.xMax - valueWidth - 4f, row.y, valueWidth, row.height), snapshot.NeedTexts[i],
                level < threshold / 2f ? ColorLibrary.RedReadable : level < threshold ? WarningColor : Color.white, TextAnchor.MiddleRight);
            float barX = row.x + 4f + shownLabel + 6f;
            Rect bar = new Rect(barX, row.y + row.height / 2f - 3f, Mathf.Max(0f, row.xMax - valueWidth - 10f - barX), 6f);
            Widgets.DrawBoxSolid(bar, new Color(0.06f, 0.08f, 0.08f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(level), bar.height),
                level < threshold / 2f ? TemperatureRed : level < threshold ? TemperatureAmber : TemperatureGreen);
            if (Mouse.IsOver(row))
            {
                TooltipHandler.TipRegion(row, new TipSignal(need.GetTipString, 0x5C1A30 + i));
            }
        }
    }

    private static void ArmorRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        string title = TextCache.Key("VUIP_PawnPaneArmourHeading");
        float titleWidth = TextCache.Width(title) + 10f;
        Label(new Rect(area.x + 3f, y, titleWidth, PawnReadout.ArmorHeaderHeight), title, LabelColor);
        Widgets.DrawBoxSolid(new Rect(area.x + titleWidth + 4f, y + PawnReadout.ArmorHeaderHeight / 2f,
            Mathf.Max(0f, area.width - titleWidth - 4f), 1f), DividerColor);
        y += PawnReadout.ArmorHeaderHeight;
        float width = (area.width - CardGap * 2f) / 3f;
        ArmorCard(new Rect(area.x, y, width, PawnReadout.ArmorCardHeight), 0,
            TextCache.Key("VUIP.PawnPaneArmorSharpShort"), snapshot.ArmorSharpText, snapshot.ArmorSharp, snapshot);
        ArmorCard(new Rect(area.x + width + CardGap, y, width, PawnReadout.ArmorCardHeight), 1,
            TextCache.Key("VUIP.PawnPaneArmorBluntShort"), snapshot.ArmorBluntText, snapshot.ArmorBlunt, snapshot);
        ArmorCard(new Rect(area.x + (width + CardGap) * 2f, y, width, PawnReadout.ArmorCardHeight), 2,
            TextCache.Key("VUIP.PawnPaneArmorHeatShort"), snapshot.ArmorHeatText, snapshot.ArmorHeat, snapshot);
        y += PawnReadout.ArmorCardHeight + PawnReadout.SectionGap;
    }

    private static void ArmorCard(Rect card, int icon, string caption, string value, float armor, PawnReadout.Snapshot snapshot)
    {
        DrawCardFrame(card);
        Widgets.DrawHighlightIfMouseover(card);
        DrawStatIcon(new Rect(card.x + 6f, card.y + 7f, 24f, 24f), icon);
        Rect text = new Rect(card.x + 36f, card.y + 1f, card.width - 40f, 20f);
        Label(text, caption, LabelColor);
        text.y += 17f;
        text.height = 20f;
        GUI.Label(text, value, ArmorValueStyle);
        Rect bar = new Rect(card.x + 7f, card.yMax - 10f, card.width - 14f, 6f);
        Widgets.DrawBoxSolid(bar, new Color(0.06f, 0.08f, 0.08f));
        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(armor), bar.height), ArmorFill);
        GUI.color = new Color(0.45f, 0.63f, 0.47f, 0.75f);
        Widgets.DrawBox(bar, 1);
        GUI.color = Color.white;
        Tip(card, snapshot, ArmorTip, 0x5C1A21 + icon);
    }

    private static void DrawCardFrame(Rect card)
    {
        Widgets.DrawBoxSolid(new Rect(card.x + 2f, card.y, card.width - 4f, card.height), CardBackground);
        Widgets.DrawBoxSolid(new Rect(card.x, card.y + 2f, 2f, card.height - 4f), CardBackground);
        Widgets.DrawBoxSolid(new Rect(card.xMax - 2f, card.y + 2f, 2f, card.height - 4f), CardBackground);
        Widgets.DrawBoxSolid(new Rect(card.x + 2f, card.y, card.width - 4f, 1f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.x + 2f, card.yMax - 1f, card.width - 4f, 1f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.x, card.y + 2f, 1f, card.height - 4f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.xMax - 1f, card.y + 2f, 1f, card.height - 4f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.x + 1f, card.y + 1f, 1f, 1f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.xMax - 2f, card.y + 1f, 1f, 1f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.x + 1f, card.yMax - 2f, 1f, 1f), CardBorder);
        Widgets.DrawBoxSolid(new Rect(card.xMax - 2f, card.yMax - 2f, 1f, 1f), CardBorder);
    }

    private static GUIStyle ArmorValueStyle
    {
        get
        {
            EnsureLanguage();
            if (armorValueStyle == null)
            {
                Text.Font = GameFont.Small;
                armorValueStyle = new GUIStyle(Text.CurFontStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
                armorValueStyle.normal.textColor = Color.white;
            }

            return armorValueStyle;
        }
    }

    private static void StatRow(Rect area, ref float y, string label, string value, Color valueColor,
        PawnReadout.Snapshot snapshot, Func<PawnReadout.Snapshot, string> tip, int tipId, int icon = -1)
    {
        Rect row = NextStatRow(area, ref y);
        if (icon >= 0)
        {
            DrawStatIcon(new Rect(row.x + 4f, row.y + 3f, 16f, 16f), icon);
        }

        float labelWidth = Mathf.Min(StatLabelWidth, row.width * 0.6f);
        Label(new Rect(row.x + IconSpace, row.y, labelWidth - IconSpace, row.height), label, LabelColor);
        Label(new Rect(row.x + labelWidth + 6f, row.y, row.width - labelWidth - 10f, row.height), value, valueColor, TextAnchor.MiddleRight);
        Tip(row, snapshot, tip, tipId);
    }

    private static void ComfortRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        Rect row = NextStatRow(area, ref y);
        DrawStatIcon(new Rect(row.x + 4f, row.y + 3f, 16f, 16f), 3);
        string caption = TextCache.Key("VUIP.PawnPaneComfort");
        float captionWidth = TextCache.Width(caption);
        Label(new Rect(row.x + IconSpace, row.y, captionWidth + 2f, row.height), caption, Color.white);
        float rangeWidth = TextCache.Width(snapshot.RangeText) + 4f;
        Rect range = new Rect(row.xMax - rangeWidth - 4f, row.y, rangeWidth, row.height);
        Label(range, snapshot.RangeText, TemperatureColor(snapshot), TextAnchor.MiddleRight);
        float stripX = row.x + IconSpace + captionWidth + 12f;
        Rect strip = new Rect(stripX, row.y + row.height / 2f - 2f, Mathf.Max(0f, range.x - 8f - stripX), 4f);
        float safeMin = Mathf.Min(snapshot.Safe.min, snapshot.Comfortable.min);
        float safeMax = Mathf.Max(snapshot.Safe.max, snapshot.Comfortable.max);
        float margin = Mathf.Max(5f, (safeMax - safeMin) * 0.1f);
        float scaleMin = safeMin - margin;
        float scaleMax = safeMax + margin;
        ThermometerZone(strip, scaleMin, scaleMax, scaleMin, safeMin, TemperatureRed);
        ThermometerZone(strip, scaleMin, scaleMax, safeMin, snapshot.Comfortable.min, TemperatureAmber);
        ThermometerZone(strip, scaleMin, scaleMax, snapshot.Comfortable.min, snapshot.Comfortable.max, TemperatureGreen);
        ThermometerZone(strip, scaleMin, scaleMax, snapshot.Comfortable.max, safeMax, TemperatureAmber);
        ThermometerZone(strip, scaleMin, scaleMax, safeMax, scaleMax, TemperatureRed);
        if (strip.width >= 4f)
        {
            float here = Mathf.Clamp(strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, snapshot.Temperature), strip.x + 1f, strip.xMax - 1f);
            Widgets.DrawBoxSolid(new Rect(here - 1.5f, strip.y - 3f, 3f, strip.height + 6f), new Color(0.08f, 0.1f, 0.1f));
            Widgets.DrawBoxSolid(new Rect(here - 0.5f, strip.y - 2f, 1f, strip.height + 4f), Color.white);
        }

        Tip(row, snapshot, TemperatureTip, 0x5C1A02);
    }

    private static void ThermometerZone(Rect strip, float min, float max, float from, float to, Color color)
    {
        float x0 = strip.x + strip.width * Mathf.InverseLerp(min, max, from);
        float x1 = strip.x + strip.width * Mathf.InverseLerp(min, max, to);
        if (x1 > x0)
        {
            Widgets.DrawBoxSolid(new Rect(x0, strip.y, x1 - x0, strip.height), color);
        }
    }

    private static Rect NextStatRow(Rect area, ref float y)
    {
        Rect row = new Rect(area.x, y, area.width, PawnReadout.RowHeight);
        y += PawnReadout.RowHeight;
        Widgets.DrawBoxSolid(new Rect(row.x, row.y, row.width, 1f), DividerColor * new Color(1f, 1f, 1f, 0.65f));
        Widgets.DrawHighlightIfMouseover(row);
        return row;
    }

    private static void Label(Rect rect, string value, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = anchor;
        Text.WordWrap = false;
        GUI.color = color;
        Widgets.Label(rect, TextCache.Truncate(value, Mathf.Max(0f, rect.width)));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;
    }

    // Tight source bounds preserve each icon's proportions and remove the atlas cell padding.
    private static void DrawStatIcon(Rect rect, int icon)
    {
        Rect source = IconPixels[icon];
        float scale = Mathf.Min(rect.width / source.width, rect.height / source.height);
        Rect target = new Rect(rect.center.x - source.width * scale / 2f, rect.center.y - source.height * scale / 2f,
            source.width * scale, source.height * scale);
        Rect uv = new Rect(source.x / ReadoutIcons.width, 1f - source.yMax / ReadoutIcons.height,
            source.width / ReadoutIcons.width, source.height / ReadoutIcons.height);
        GUI.DrawTextureWithTexCoords(target, ReadoutIcons, uv, true);
    }

    private static void Tip(Rect rect, PawnReadout.Snapshot snapshot, Func<PawnReadout.Snapshot, string> tip, int tipId)
    {
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, new TipSignal(() => tip(snapshot), tipId));
        }
    }

    private static void EnsureLanguage()
    {
        if (measuredLanguage == LanguageDatabase.activeLanguage && measuredScale == Prefs.UIScale)
        {
            return;
        }

        measuredLanguage = LanguageDatabase.activeLanguage;
        measuredScale = Prefs.UIScale;
        statLabelWidth = -1f;
        minStatsWidth = -1f;
        armorValueStyle = null;
        SkillLabels.Clear();
    }

    private static float StatLabelWidth
    {
        get
        {
            EnsureLanguage();
            if (statLabelWidth < 0f)
            {
                string[] labels = { "VUIP.PawnPaneArmor", "VUIP.PawnPaneComfort", "VUIP.PawnPaneMovement", "VUIP.PawnPaneWorkSpeed",
                    "VUIP.PawnPaneMeleeDps", "VUIP.PawnPaneRangedDps", "VUIP.PawnPaneBleeding", "VUIP.PawnPaneLowNeeds" };
                foreach (string label in labels)
                {
                    statLabelWidth = Mathf.Max(statLabelWidth, TextCache.Width(TextCache.Key(label)));
                }

                statLabelWidth += IconSpace + 4f;
            }

            return statLabelWidth;
        }
    }

    public static float MinStatsWidth
    {
        get
        {
            EnsureLanguage();
            if (minStatsWidth < 0f)
            {
                MeasureContent.text = "200%";
                float valueWidth = ArmorValueStyle.CalcSize(MeasureContent).x;
                float captionWidth = Mathf.Max(TextCache.Width(TextCache.Key("VUIP.PawnPaneArmorSharpShort")),
                    TextCache.Width(TextCache.Key("VUIP.PawnPaneArmorBluntShort")), TextCache.Width(TextCache.Key("VUIP.PawnPaneArmorHeatShort")));
                float armor = 3f * (40f + Mathf.Max(captionWidth, valueWidth)) + CardGap * 2f;
                float comfort = IconSpace + TextCache.Width(TextCache.Key("VUIP.PawnPaneComfort")) + 28f + ThermometerMinWidth
                    + TextCache.Width(PawnReadout.TemperatureRange(-199f, 199f));
                minStatsWidth = Mathf.Max(armor, comfort, StatLabelWidth + TextCache.Width("188%") + 12f);
            }

            // Unusual races and apparel can have wider temperature ranges than the default measure.
            Pawn? pawn = PawnReadout.SelectedPawn();
            if (pawn != null && UiPlusMod.Settings.pawnPaneShowTemperature)
            {
                float current = IconSpace + TextCache.Width(TextCache.Key("VUIP.PawnPaneComfort")) + 28f + ThermometerMinWidth
                    + TextCache.Width(PawnReadout.For(pawn).RangeText);
                return Mathf.Max(minStatsWidth, current);
            }

            return minStatsWidth;
        }
    }

    public static float MinPaneWidth(bool withSkills)
    {
        return MinStatsWidth + (withSkills ? ColumnGap + SkillsWidth : 0f) + 24f;
    }
}
