using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static partial class PawnReadoutDrawer
{
    private const float CardGap = 4f;
    private const float IconSpace = 28f;
    // Icon index for low-need rows: the needs view texture rather than an atlas cell.
    public const int NeedIcon = 12;
    private static readonly Color CardBorder = new Color(0.36f, 0.42f, 0.45f, 0.8f);
    private static readonly Color CardBackground = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color TemperatureGreen = new Color(0.57f, 0.76f, 0.48f);
    private static readonly Color TemperatureAmber = new Color(0.95f, 0.74f, 0.34f);
    private static readonly Color TemperatureRed = new Color(0.9f, 0.37f, 0.43f);
    private static readonly Texture2D ReadoutIcons = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/PawnReadoutIcons");
    private static readonly Rect[] IconPixels =
    {
        new Rect(130f, 111f, 230f, 268f), new Rect(580f, 99f, 212f, 287f),
        new Rect(1015f, 98f, 213f, 291f), new Rect(1464f, 98f, 154f, 283f),
        new Rect(145f, 519f, 221f, 262f), new Rect(557f, 532f, 251f, 249f),
        new Rect(967f, 512f, 279f, 278f), new Rect(1437f, 507f, 217f, 287f),
        new Rect(102f, 957f, 287f, 287f), new Rect(562f, 962f, 237f, 267f),
        new Rect(982f, 972f, 257f, 257f), new Rect(1457f, 957f, 177f, 287f)
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
            if (PawnReadout.ArmorUnavailable)
            {
                StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneArmor"), "-", LabelColor, snapshot,
                    _ => TextCache.Key("VUIP.PawnPaneArmorCombatExtended"), 0x5C1A01, 1);
            }
            else
            {
                ArmorRow(rect, ref y, snapshot);
            }
        }

        if (settings.pawnPaneShowSpeed || settings.pawnPaneShowDps)
        {
            StatCardRow(rect, ref y, snapshot);
        }

        for (int i = 0; i < snapshot.HealthRows.Count; i++)
        {
            HealthRow(rect, ref y, snapshot.HealthRows[i], snapshot, 0x5C1A40 + i);
        }

        for (int i = 0; i < snapshot.NeedRows.Count; i++)
        {
            HealthRow(rect, ref y, snapshot.NeedRows[i], snapshot, 0x5C1A60 + i);
        }
    }

    // Label, a bar (two stacked for a disease: severity over immunity) and the value when there is one.
    private static void HealthRow(Rect area, ref float y, PawnReadout.HealthRow health, PawnReadout.Snapshot snapshot, int tipId)
    {
        Rect row = NextStatRow(area, ref y);
        if (health.Icon >= 0)
        {
            DrawStatIcon(new Rect(row.x + 4f, row.y + 3f, 16f, 16f), health.Icon);
        }

        Color color = health.Level switch
        {
            PawnReadout.HealthLevel.Good => TemperatureGreen,
            PawnReadout.HealthLevel.Warning => WarningColor,
            _ => ColorLibrary.RedReadable,
        };
        // Rows without an icon start their label at the row edge, in line with the cards.
        float labelX = health.Icon >= 0 ? IconSpace : 4f;
        float labelWidth = TextCache.Width(health.Label) + 4f;
        Label(new Rect(row.x + labelX, row.y, Mathf.Min(labelWidth, row.width * 0.45f), row.height), health.Label, LabelColor);
        float valueX = row.xMax;
        if (!health.Value.NullOrEmpty())
        {
            float valueWidth = TextCache.Width(health.Value) + 4f;
            Rect value = new Rect(row.xMax - valueWidth - 4f, row.y, valueWidth, row.height);
            Label(value, health.Value, color, TextAnchor.MiddleRight);
            valueX = value.x - 4f;
        }

        if (health.Bar.HasValue)
        {
            float barX = row.x + Mathf.Max(StatLabelWidth, labelX + Mathf.Min(labelWidth, row.width * 0.45f) + 6f);
            float barWidth = Mathf.Max(0f, valueX - 4f - barX);
            if (health.SecondBar.HasValue)
            {
                HealthBar(new Rect(barX, row.y + row.height / 2f - 4f, barWidth, 3f), health.Bar.Value, TemperatureRed);
                HealthBar(new Rect(barX, row.y + row.height / 2f + 1f, barWidth, 3f), health.SecondBar.Value, TemperatureGreen);
            }
            else
            {
                HealthBar(new Rect(barX, row.y + row.height / 2f - 3f, barWidth, 6f), health.Bar.Value,
                    health.Level == PawnReadout.HealthLevel.Critical ? TemperatureRed : TemperatureAmber);
            }
        }

        Tip(row, snapshot, _ => health.Tip(), tipId);
    }

    private static void HealthBar(Rect bar, float fill, Color color)
    {
        Widgets.DrawBoxSolid(bar, new Color(0.06f, 0.08f, 0.08f));
        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), color);
    }

    // One row per need: label, a bar colored by the low-needs threshold, and the level.
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
        Card(CardSlot(area, y, 0), 0, TextCache.Key("VUIP.PawnPaneArmorSharpShort"), snapshot.ArmorSharpText, Color.white, snapshot, ArmorTip, 0x5C1A21);
        Card(CardSlot(area, y, 1), 1, TextCache.Key("VUIP.PawnPaneArmorBluntShort"), snapshot.ArmorBluntText, Color.white, snapshot, ArmorTip, 0x5C1A22);
        Card(CardSlot(area, y, 2), 2, TextCache.Key("VUIP.PawnPaneArmorHeatShort"), snapshot.ArmorHeatText, Color.white, snapshot, ArmorTip, 0x5C1A23);
        y += PawnReadout.ArmorCardHeight + PawnReadout.SectionGap;
    }

    // Movement, work speed and damage per second as cards under the armor, filling slots from the left.
    private static void StatCardRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        int slot = 0;
        if (settings.pawnPaneShowSpeed)
        {
            Card(CardSlot(area, y, slot++), 4, TextCache.Key("VUIP.PawnPaneMovementShort"), snapshot.MoveText, Color.white, snapshot,
                s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed), 0x5C1A03);
            Card(CardSlot(area, y, slot++), 5, TextCache.Key("VUIP.PawnPaneWorkSpeedShort"), snapshot.WorkText, Color.white, snapshot,
                s => StatTip(s.Pawn, StatDefOf.WorkSpeedGlobal, s.WorkSpeed), 0x5C1A09);
        }

        if (settings.pawnPaneShowDps)
        {
            Card(CardSlot(area, y, slot), 6, snapshot.Ranged ? TextCache.Key("VUIP.PawnPaneRangedDpsShort") : TextCache.Key("VUIP.PawnPaneMeleeDpsShort"),
                snapshot.DpsText, snapshot.Ranged && snapshot.Dps < 0f ? LabelColor : Color.white, snapshot, CombatTip, 0x5C1A04);
        }

        y += PawnReadout.ArmorCardHeight + PawnReadout.SectionGap;
    }

    private static Rect CardSlot(Rect area, float y, int slot)
    {
        float width = (area.width - CardGap * 2f) / 3f;
        return new Rect(area.x + (width + CardGap) * slot, y, width, PawnReadout.ArmorCardHeight);
    }

    private static void Card(Rect card, int icon, string caption, string value, Color valueColor,
        PawnReadout.Snapshot snapshot, Func<PawnReadout.Snapshot, string> tip, int tipId)
    {
        DrawCardFrame(card);
        Widgets.DrawHighlightIfMouseover(card);
        DrawStatIcon(new Rect(card.x + 6f, card.center.y - 12f, 24f, 24f), icon);
        Rect text = new Rect(card.x + 36f, card.center.y - 19f, card.width - 40f, 20f);
        Label(text, caption, LabelColor);
        text.y += 17f;
        GUI.color = valueColor;
        GUI.Label(text, value, ArmorValueStyle);
        GUI.color = Color.white;
        Tip(card, snapshot, tip, tipId);
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
        if (icon == NeedIcon)
        {
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            GUI.DrawTexture(rect, NeedsViewTex, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            return;
        }

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
                string[] labels = { "VUIP.PawnPaneArmor", "VUIP.PawnPaneBleeding", "VUIP.PawnPaneNeedsTending", "VUIP.PawnPanePain" };
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
                // Card values: percentages, and plain numbers for movement, damage and Combat Extended armor.
                float valueWidth = 0f;
                foreach (string sample in new[] { "200%", "99.99" })
                {
                    MeasureContent.text = sample;
                    valueWidth = Mathf.Max(valueWidth, ArmorValueStyle.CalcSize(MeasureContent).x);
                }

                float captionWidth = 0f;
                foreach (string key in new[] { "VUIP.PawnPaneArmorSharpShort", "VUIP.PawnPaneArmorBluntShort", "VUIP.PawnPaneArmorHeatShort",
                    "VUIP.PawnPaneMovementShort", "VUIP.PawnPaneWorkSpeedShort", "VUIP.PawnPaneMeleeDpsShort", "VUIP.PawnPaneRangedDpsShort" })
                {
                    captionWidth = Mathf.Max(captionWidth, TextCache.Width(TextCache.Key(key)));
                }

                float cards = 3f * (40f + Mathf.Max(captionWidth, valueWidth)) + CardGap * 2f;
                minStatsWidth = Mathf.Max(cards, StatLabelWidth + TextCache.Width("188%") + 12f);
            }

            return minStatsWidth;
        }
    }

    public static float MinPaneWidth(bool withSkills)
    {
        return MinStatsWidth + (withSkills ? ColumnGap + SkillsWidth : 0f) + 24f;
    }
}
