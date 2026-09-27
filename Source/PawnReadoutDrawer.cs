using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Draws the pawn inspect pane readout: the base game's bar row, stats on the left, skills on the right, and an activity line.
/// </summary>
[StaticConstructorOnStartup]
public static class PawnReadoutDrawer
{
    private const float SkillsWidth = 272f;
    private const float ColumnGap = 12f;
    private const float SkillGap = 8f;

    private const float LevelWidth = 22f;
    private const float PassionSize = 14f;

    private const float CellGap = 4f;
    private const float CellPad = 3f;
    private const float GaugeHeight = 2f;
    private const float CellSlack = 6f;
    private const float ComfortScaleMargin = 5f;

    private static readonly Color WarningColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color LabelColor = SettingsWidgets.MutedColor;
    private static readonly Color GaugeTrackColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color GaugeFillColor = new Color(1f, 1f, 1f, 0.35f);

    // Same tints as the skill bars' aptitude colours, stronger so a thin strip still reads.
    private static readonly Color GaugeAboveColor = new Color(0.8f, 1f, 0.6f, 0.6f);
    private static readonly Color GaugeBelowColor = new Color(1f, 0.5f, 0.6f, 0.6f);
    private static readonly Color GaugeCentreColor = new Color(1f, 1f, 1f, 0.4f);
    private static readonly Color GaugeWarningColor = new Color(1f, 0.8f, 0.35f, 0.6f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color DisabledSkillColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Texture2D SkillBarFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 1f, 1f, 0.1f));
    private static readonly Texture2D SkillBarAptitudePositiveTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 1f, 0.6f, 0.25f));
    private static readonly Texture2D SkillBarAptitudeNegativeTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.5f, 0.6f, 0.25f));

    // Same background as the base game's inspect pane bars (InspectPaneFiller).
    private static readonly Texture2D BarBGTex = SolidColorMaterials.NewSolidColorTexture(new ColorInt(10, 10, 10).ToColor);

    // Private base game drawers for the bar row, so it behaves exactly as in the base game (the area bar opens its menu).
    private static readonly Action<WidgetRow, Pawn>? DrawMood = RowDrawer("DrawMood");
    private static readonly Action<WidgetRow, Pawn>? DrawTimetableSetting = RowDrawer("DrawTimetableSetting");
    private static readonly Action<WidgetRow, Pawn>? DrawAreaAllowed = RowDrawer("DrawAreaAllowed");
    private static readonly Func<SkillRecord, string>? SkillDescription =
        AccessTools.Method(typeof(SkillUI), "GetSkillDescription") is { } method
            ? AccessTools.MethodDelegate<Func<SkillRecord, string>>(method)
            : null;

    private static readonly Texture2D SelfTendTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/SelfTend");
    private static readonly Texture2D SelfTendOffTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/SelfTendOff");
    private static readonly Color SelfTendOffColor = new Color(1f, 1f, 1f, 0.3f);

    // Self-tend toggle in the pane's header buttons, with the same rules as the Health tab's checkbox (HealthCardUtility).
    public static void DrawSelfTendButton(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (!pawn.IsColonist || pawn.playerSettings == null || pawn.DevelopmentalStage.Baby())
        {
            return;
        }

        Rect button = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
        lineEndWidth += 24f;
        bool canDoctor = !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
        bool on = canDoctor && pawn.playerSettings.selfTend;
        string state = on ? "VUIP.PawnPaneSelfTendOn".Translate() : "VUIP.PawnPaneSelfTendOff".Translate();
        string tip = "AllowSelfTend".Translate().CapitalizeFirst().Resolve().AsTipTitle() + ": " + state + "\n\n"
            + (canDoctor
                ? "AllowSelfTendTip".Translate(Faction.OfPlayer.def.pawnsPlural, 0.7f.ToStringPercent()).CapitalizeFirst().Resolve()
                : "MessageCannotSelfTendEver".Translate(pawn.LabelShort, pawn).Resolve());
        TooltipHandler.TipRegion(button, tip);
        if (!canDoctor)
        {
            GUI.color = SelfTendOffColor;
            GUI.DrawTexture(button.ContractedBy(2f), SelfTendOffTex);
            GUI.color = Color.white;
            return;
        }

        // Filled cross when on, outline when off.
        if (Widgets.ButtonImage(button.ContractedBy(2f), on ? SelfTendTex : SelfTendOffTex, Color.white, GenUI.MouseoverColor))
        {
            pawn.playerSettings.selfTend = !on;
            if (!on)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                if (pawn.workSettings != null && pawn.workSettings.GetPriority(WorkTypeDefOf.Doctor) == 0)
                {
                    Messages.Message("MessageSelfTendUnsatisfied".Translate(pawn.LabelShort, pawn), MessageTypeDefOf.CautionInput, historical: false);
                }
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
        }
    }

    // Age, gender and xenotype left of the header buttons, laid out right to left; tooltips and genes click match the Bio tab (CharacterCardUtility).
    public static void DrawIdentityIcons(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (pawn.ageTracker != null)
        {
            string age = pawn.ageTracker.AgeBiologicalYears.ToString();
            Text.Font = GameFont.Small;
            float width = Text.CalcSize(age).x + 6f;
            Rect ageRect = new Rect(paneRect.width - lineEndWidth - width, 0f, width, 24f);
            lineEndWidth += width;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(ageRect, age);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(ageRect, () => pawn.ageTracker.AgeTooltipString, 0x5C1A10);
        }

        if (pawn.gender != Gender.None)
        {
            Rect genderRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            GUI.DrawTexture(genderRect.ContractedBy(2f), pawn.gender.GetIcon());
            TooltipHandler.TipRegion(genderRect, () => pawn.gender.GetLabel(pawn.AnimalOrWildMan()).CapitalizeFirst(), 0x5C1A11);
        }

        if (ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.GenesListForReading.Any())
        {
            Rect xenoRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            TooltipHandler.TipRegion(xenoRect, () => ("Xenotype".Translate() + ": " + pawn.genes.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor)
                + "\n\n" + pawn.genes.XenotypeDescShort + "\n\n"
                + "ViewGenesDesc".Translate(pawn.Named("PAWN")).ToString().StripTags().Colorize(ColoredText.SubtleGrayColor), 0x5C1A12);
            // Full rect: xenotype textures carry more padding than the other header icons.
            if (Widgets.ButtonImage(xenoRect, pawn.genes.XenotypeIcon, Color.white, GenUI.MouseoverColor))
            {
                InspectPaneUtility.OpenTab(typeof(ITab_Genes));
            }
        }
    }

    public static void Draw(Pawn pawn, Rect rect)
    {
        Widgets.BeginGroup(rect);
        try
        {
            DrawTopBar(pawn);
            PawnReadout.Snapshot snapshot = PawnReadout.For(pawn);
            Rect body = new Rect(0f, PawnReadout.TopBarHeight + PawnReadout.SectionGap, rect.width, PawnReadout.BodyRows * PawnReadout.RowHeight);
            Rect stats = body;
            if (UiPlusMod.Settings.pawnPaneShowSkills)
            {
                float skillsWidth = Mathf.Min(SkillsWidth, body.width * 0.55f);
                Rect skills = new Rect(body.xMax - skillsWidth, body.y, skillsWidth, body.height);
                stats.xMax = skills.x - ColumnGap;
                GUI.color = DividerColor;
                Widgets.DrawLineVertical(skills.x - ColumnGap / 2f, body.y, body.height);
                GUI.color = Color.white;
                DrawSkills(pawn, skills);
            }

            DrawStats(snapshot, stats);
            Rect footer = new Rect(0f, body.yMax + PawnReadout.SectionGap, rect.width, PawnReadout.FooterHeight);
            GUI.color = DividerColor;
            Widgets.DrawLineHorizontal(0f, footer.y - PawnReadout.SectionGap / 2f, rect.width);
            GUI.color = Color.white;
            DrawFooter(pawn, footer);
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] Error drawing pawn readout for {pawn}: {exception}", 0x5C1A7E);
        }
        finally
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndGroup();
        }
    }

    // Same bars as the base game's first row (InspectPaneFiller.DoPaneContentsFor).
    private static void DrawTopBar(Pawn pawn)
    {
        WidgetRow row = new WidgetRow(0f, 3f);
        if (UiPlusMod.Settings.pawnPaneColorHealthBar)
        {
            row.FillableBar(93f, 16f, pawn.health.summaryHealth.SummaryHealthPercent, HealthUtility.GetGeneralConditionLabel(pawn, shortVersion: true),
                StatusBarColors.HealthTexture(pawn), BarBGTex);
        }
        else
        {
            InspectPaneFiller.DrawHealth(row, pawn);
        }

        if (UiPlusMod.Settings.pawnPaneColorMoodBar && pawn.needs?.mood != null && pawn.mindState?.mentalBreaker != null)
        {
            row.Gap(6f);
            row.FillableBar(93f, 16f, pawn.needs.mood.CurLevelPercentage, pawn.needs.mood.MoodString.CapitalizeFirst(), StatusBarColors.MoodTexture(pawn), BarBGTex);
        }
        else
        {
            DrawMood?.Invoke(row, pawn);
        }
        if (pawn.timetable != null && !pawn.IsPrisonerOfColony)
        {
            DrawTimetableSetting?.Invoke(row, pawn);
        }

        DrawAreaAllowed?.Invoke(row, pawn);
    }

    private static void DrawStats(PawnReadout.Snapshot snapshot, Rect rect)
    {
        Pawn pawn = snapshot.Pawn;
        UiPlusSettings settings = UiPlusMod.Settings;
        float y = rect.y;
        if (settings.pawnPaneShowArmor)
        {
            if (PawnReadout.CombatExtendedActive)
            {
                StatRow(rect, ref y, "VUIP.PawnPaneArmor".Translate(), "-", SettingsWidgets.MutedColor,
                    () => "VUIP.PawnPaneArmorCombatExtended".Translate(), 0x5C1A01);
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

        if (settings.pawnPaneShowSpeed && NextRow(rect, ref y, out Rect speedRow, out Rect speedValues))
        {
            LabelStat(speedRow, "VUIP.PawnPaneSpeed".Translate());
            Rect[] cells = Cells(speedValues, 2);
            StatDef move = StatDefOf.MoveSpeed;
            StatDef work = StatDefOf.WorkSpeedGlobal;
            StatCell(cells[0], "VUIP.PawnPaneMoveShort".Translate(), snapshot.MoveSpeed.ToString("0.00"), Color.white,
                snapshot.MoveSpeed / Mathf.Max(0.01f, snapshot.BaseMoveSpeed),() => StatTip(pawn, move, snapshot.MoveSpeed), 0x5C1A03, centred: true);
            StatCell(cells[1], "VUIP.PawnPaneWorkShort".Translate(), work.ValueToString(snapshot.WorkSpeed), Color.white,
                snapshot.WorkSpeed, () => StatTip(pawn, work, snapshot.WorkSpeed), 0x5C1A09, centred: true);
        }

        if (settings.pawnPaneShowDps && NextRow(rect, ref y, out Rect combatRow, out Rect combatValues))
        {
            LabelStat(combatRow, snapshot.Ranged ? "VUIP.PawnPaneRanged".Translate() : "VUIP.PawnPaneMelee".Translate());
            if (snapshot.Ranged)
            {
                bool known = snapshot.Dps >= 0f;
                string tip = (snapshot.RangedTip ?? (PawnReadout.CombatExtendedActive
                    ? "VUIP.PawnPaneRangedDpsCombatExtended".Translate().Resolve()
                    : "VUIP.PawnPaneRangedDpsUnknown".Translate().Resolve())) + "\n\n" + snapshot.HitTip;
                StatCell(combatValues, "VUIP.PawnPaneDpsShort".Translate(), known ? snapshot.Dps.ToString("0.0") : "-", known ? Color.white : SettingsWidgets.MutedColor,
                    snapshot.HitChance, () => tip, 0x5C1A04);
            }
            else
            {
                StatDef dps = StatDefOf.MeleeDPS;
                StatDef hit = StatDefOf.MeleeHitChance;
                StatCell(combatValues, "VUIP.PawnPaneDpsShort".Translate(), dps.ValueToString(snapshot.Dps), Color.white, snapshot.HitChance,
                    () => StatTip(pawn, dps, snapshot.Dps) + "\n\n" + StatTip(pawn, hit, snapshot.HitChance), 0x5C1A05);
            }
        }

        if (snapshot.BleedRate > 0f)
        {
            string bleeding = snapshot.TicksToBleedOut > 0 && snapshot.TicksToBleedOut < int.MaxValue
                ? "VUIP.PawnPaneBleedingValue".Translate(snapshot.TicksToBleedOut.ToStringTicksToPeriod())
                : "VUIP.PawnPaneBleedingRate".Translate(snapshot.BleedRate.ToStringPercent());
            StatRow(rect, ref y, "VUIP.PawnPaneBleeding".Translate(), bleeding, ColorLibrary.RedReadable, null, 0x5C1A06);
        }

        if (snapshot.LowNeeds.Count > 0)
        {
            float threshold = settings.pawnPaneNeedThreshold / 100f;
            bool critical = false;
            StringBuilder sb = new StringBuilder();
            foreach (Need need in snapshot.LowNeeds)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(need.LabelCap).Append(' ').Append(need.CurLevelPercentage.ToStringPercent());
                critical |= need.CurLevelPercentage < threshold / 2f;
            }

            StatRow(rect, ref y, "VUIP.PawnPaneLowNeeds".Translate(), sb.ToString(), critical ? ColorLibrary.RedReadable : WarningColor,
                () => NeedsTip(snapshot), 0x5C1A07);
        }
    }

    // Label on the left, value right-aligned like the skill levels; the tooltip covers the row.
    private static void StatRow(Rect area, ref float y, string label, string value, Color valueColor, Func<string>? tip, int tipId)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, label);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = valueColor;
        Widgets.Label(valueRect, TextCache.Truncate(value, valueRect.width));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        if (tip != null)
        {
            TooltipHandler.TipRegion(row, new TipSignal(tip, tipId));
        }
    }

    // Sharp, blunt and heat in three cells; bars fill against the 200% armor cap.
    private static void ArmorRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, "VUIP.PawnPaneArmor".Translate());
        Rect[] cells = Cells(valueRect, 3);
        ArmorCell(cells[0], "VUIP.PawnPaneArmorSharpShort".Translate(), "ArmorSharp".Translate(), snapshot.ArmorSharp, 0x5C1A11);
        ArmorCell(cells[1], "VUIP.PawnPaneArmorBluntShort".Translate(), "ArmorBlunt".Translate(), snapshot.ArmorBlunt, 0x5C1A12);
        ArmorCell(cells[2], "VUIP.PawnPaneArmorHeatShort".Translate(), "ArmorHeat".Translate(), snapshot.ArmorHeat, 0x5C1A13);
        TooltipHandler.TipRegion(new Rect(row.x, row.y, valueRect.x - row.x, row.height), new TipSignal(() => ArmorTip(snapshot), 0x5C1A01));
    }

    private static void ArmorCell(Rect cell, string initial, string name, float value, int tipId)
    {
        string tip = name + ": " + value.ToStringPercent();
        StatCell(cell, initial, value.ToStringPercent("F0"), Color.white, value / 2f, () => tip, tipId);
    }

    // A thermometer scaled to this pawn's safe range plus a margin: red beyond safe, amber between safe and comfortable, green when comfortable, and a white tick (pinned to the edge when off the scale) for the temperature where the pawn stands.
    private static void ComfortRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, "VUIP.PawnPaneComfort".Translate());
        Rect strip = GaugeStrip(valueRect);
        float safeMin = Mathf.Min(snapshot.Safe.min, snapshot.Comfortable.min);
        float safeMax = Mathf.Max(snapshot.Safe.max, snapshot.Comfortable.max);
        float margin = Mathf.Max(ComfortScaleMargin, (safeMax - safeMin) * 0.1f);
        float scaleMin = safeMin - margin;
        float scaleMax = safeMax + margin;
        ThermometerZone(strip, scaleMin, scaleMax, scaleMin, safeMin, GaugeBelowColor);
        ThermometerZone(strip, scaleMin, scaleMax, safeMin, snapshot.Comfortable.min, GaugeWarningColor);
        ThermometerZone(strip, scaleMin, scaleMax, snapshot.Comfortable.min, snapshot.Comfortable.max, GaugeAboveColor);
        ThermometerZone(strip, scaleMin, scaleMax, snapshot.Comfortable.max, safeMax, GaugeWarningColor);
        ThermometerZone(strip, scaleMin, scaleMax, safeMax, scaleMax, GaugeBelowColor);
        Color color = TemperatureColor(snapshot);
        float here = Mathf.Clamp(strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, snapshot.Temperature), strip.x + 1f, strip.xMax - 1f);
        Widgets.DrawBoxSolid(new Rect(here - 1f, strip.y - 2f, 2f, strip.height + 3f), Color.white);

        // The range sits centred over the green comfortable zone, kept inside the value area.
        string range = TextCache.Truncate(TemperatureRange(snapshot.Comfortable.min, snapshot.Comfortable.max), valueRect.width - CellPad * 2f);
        float rangeWidth = Text.CalcSize(range).x;
        float comfortCentre = strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, (snapshot.Comfortable.min + snapshot.Comfortable.max) / 2f);
        float rangeX = Mathf.Clamp(comfortCentre - rangeWidth / 2f, valueRect.x + CellPad, valueRect.xMax - CellPad - rangeWidth);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = color;
        Widgets.Label(new Rect(rangeX, valueRect.y, rangeWidth + 1f, valueRect.height - GaugeHeight), range);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(row, new TipSignal(() => TemperatureTip(snapshot), 0x5C1A02));
    }

    // "-14 ~ 39°C": both ends in the player's temperature unit, with the unit written once.
    private static string TemperatureRange(float minCelsius, float maxCelsius)
    {
        TemperatureDisplayMode mode = Prefs.TemperatureMode;
        string unit = mode switch
        {
            TemperatureDisplayMode.Fahrenheit => "°F",
            TemperatureDisplayMode.Kelvin => "K",
            _ => "°C",
        };
        return GenTemperature.CelsiusTo(minCelsius, mode).ToString("F0") + " ~ " + GenTemperature.CelsiusTo(maxCelsius, mode).ToString("F0") + unit;
    }

    private static void ThermometerZone(Rect strip, float scaleMin, float scaleMax, float from, float to, Color color)
    {
        float x0 = strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, from);
        float x1 = strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, to);
        if (x1 > x0)
        {
            Widgets.DrawBoxSolid(new Rect(x0, strip.y, x1 - x0, strip.height), color);
        }
    }

    // Splits a value area into equal cells with a small gap so their bars stay apart.
    private static Rect[] Cells(Rect area, int count)
    {
        float width = (area.width - CellGap * (count - 1)) / count;
        Rect[] cells = new Rect[count];
        for (int i = 0; i < count; i++)
        {
            cells[i] = new Rect(area.x + i * (width + CellGap), area.y, width, area.height);
        }

        return cells;
    }

    // Grey caption on the left, never cut short; the value on the right; a thin gauge along the bottom.
    // With centred set, fill is the value divided by normal: the gauge grows right in green above normal (full at double) and left in red below (full at zero).
    private static void StatCell(Rect cell, string caption, string value, Color valueColor, float fill, Func<string> tip, int tipId, bool centred = false)
    {
        Rect strip = GaugeStrip(cell);
        Widgets.DrawBoxSolid(strip, GaugeTrackColor);
        if (centred)
        {
            float half = strip.width / 2f;
            float mid = strip.x + half;
            if (fill >= 1f)
            {
                Widgets.DrawBoxSolid(new Rect(mid, strip.y, half * Mathf.Clamp01(fill - 1f), strip.height), GaugeAboveColor);
            }
            else
            {
                float width = half * Mathf.Clamp01(1f - fill);
                Widgets.DrawBoxSolid(new Rect(mid - width, strip.y, width, strip.height), GaugeBelowColor);
            }

            Widgets.DrawBoxSolid(new Rect(mid - 0.5f, strip.y - 1f, 1f, strip.height + 2f), GaugeCentreColor);
        }
        else if (fill > 0f)
        {
            Widgets.DrawBoxSolid(new Rect(strip.x, strip.y, strip.width * Mathf.Clamp01(fill), strip.height), GaugeFillColor);
        }

        Rect inner = new Rect(cell.x + CellPad, cell.y, cell.width - CellPad * 2f, cell.height - GaugeHeight);
        Text.Font = GameFont.Small;
        float captionWidth = Text.CalcSize(caption).x;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = LabelColor;
        Widgets.Label(new Rect(inner.x, inner.y, captionWidth + 1f, inner.height), caption);

        Rect valueRect = new Rect(inner.x + captionWidth + 3f, inner.y, Mathf.Max(0f, inner.width - captionWidth - 3f), inner.height);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = valueColor;
        Widgets.Label(valueRect, TextCache.Truncate(value, valueRect.width));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(cell, new TipSignal(tip, tipId));
    }

    // The bottom edge of a cell or value area, where the gauge track and fill are drawn.
    private static Rect GaugeStrip(Rect area)
    {
        return new Rect(area.x, area.yMax - GaugeHeight - 1f, area.width, GaugeHeight);
    }

    private static float statLabelWidth = -1f;

    // Widest stat label plus padding, measured once so no label is cut off.
    private static float StatLabelWidth
    {
        get
        {
            if (statLabelWidth < 0f)
            {
                Text.Font = GameFont.Small;
                string[] labels =
                {
                    "VUIP.PawnPaneArmor".Translate(), "VUIP.PawnPaneComfort".Translate(), "VUIP.PawnPaneSpeed".Translate(),
                    "VUIP.PawnPaneMelee".Translate(), "VUIP.PawnPaneRanged".Translate(), "VUIP.PawnPaneBleeding".Translate(),
                    "VUIP.PawnPaneLowNeeds".Translate()
                };
                foreach (string label in labels)
                {
                    statLabelWidth = Mathf.Max(statLabelWidth, Text.CalcSize(label).x);
                }

                statLabelWidth += 12f;
            }

            return statLabelWidth;
        }
    }

    private static float minStatsWidth = -1f;

    // Width the stats column needs so every caption and its widest value fit, measured once.
    public static float MinStatsWidth
    {
        get
        {
            if (minStatsWidth < 0f)
            {
                GameFont font = Text.Font;
                Text.Font = GameFont.Small;
                float armor = 3f * Mathf.Max(CellWidthFor("VUIP.PawnPaneArmorSharpShort".Translate(), "188%"),
                    CellWidthFor("VUIP.PawnPaneArmorBluntShort".Translate(), "188%"),
                    CellWidthFor("VUIP.PawnPaneArmorHeatShort".Translate(), "188%")) + CellGap * 2f;
                float speed = 2f * Mathf.Max(CellWidthFor("VUIP.PawnPaneMoveShort".Translate(), "8.88"),
                    CellWidthFor("VUIP.PawnPaneWorkShort".Translate(), "188%")) + CellGap;
                float combat = CellWidthFor("VUIP.PawnPaneDpsShort".Translate(), "88.8");
                float comfort = Text.CalcSize(TemperatureRange(-99f, 99f)).x + CellPad;
                minStatsWidth = StatLabelWidth + Mathf.Max(armor, speed, combat, comfort) + 2f;
                Text.Font = font;
            }

            return minStatsWidth;
        }
    }

    // Pane width that fits the stats column next to the skills: stats, divider gap and skills plus the pane's 12px margins.
    public static float MinPaneWidth(bool withSkills)
    {
        return MinStatsWidth + (withSkills ? ColumnGap + SkillsWidth : 0f) + 24f;
    }

    private static float CellWidthFor(string caption, string widestValue)
    {
        return CellPad * 2f + Text.CalcSize(caption).x + 3f + Text.CalcSize(widestValue).x + CellSlack;
    }

    private static bool NextRow(Rect area, ref float y, out Rect row, out Rect valueRect)
    {
        row = new Rect(area.x, y, area.width, PawnReadout.RowHeight);
        float labelWidth = Mathf.Min(StatLabelWidth, row.width * 0.5f);
        valueRect = new Rect(row.x + labelWidth, row.y, row.width - labelWidth - 2f, row.height);
        if (y + PawnReadout.RowHeight > area.yMax + 0.5f)
        {
            return false;
        }

        y += PawnReadout.RowHeight;
        Widgets.DrawHighlightIfMouseover(row);
        return true;
    }

    private static void LabelStat(Rect row, string label)
    {
        float labelWidth = Mathf.Min(StatLabelWidth, row.width * 0.5f);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = LabelColor;
        Widgets.Label(new Rect(row.x + 4f, row.y, labelWidth - 4f, row.height), TextCache.Truncate(label, labelWidth - 4f));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void DrawSkills(Pawn pawn, Rect rect)
    {
        if (pawn.DevelopmentalStage.Baby())
        {
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "SkillsDevelopLaterBaby".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        List<SkillDef> skills = PawnReadout.SkillsInOrder;
        float cellWidth = (rect.width - SkillGap) / 2f;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord? skill = pawn.skills.GetSkill(skills[i]);
            if (skill == null)
            {
                continue;
            }

            Rect cell = new Rect(rect.x + (i % 2) * (cellWidth + SkillGap), rect.y + (i / 2) * PawnReadout.RowHeight, cellWidth, PawnReadout.RowHeight);
            if (cell.yMax > rect.yMax + 0.5f)
            {
                break;
            }

            DrawSkill(skill, cell);
        }
    }

    // A compact version of the Bio tab's skill row (SkillUI.DrawSkill): bar, label, passion and level.
    private static void DrawSkill(SkillRecord skill, Rect cell)
    {
        Widgets.DrawHighlightIfMouseover(cell);
        int level = skill.GetLevel();
        bool disabled = skill.TotallyDisabled;
        if (!disabled)
        {
            Texture2D fill = SkillBarFillTex;
            if ((ModsConfig.BiotechActive || ModsConfig.AnomalyActive) && skill.Aptitude != 0)
            {
                fill = skill.Aptitude > 0 ? SkillBarAptitudePositiveTex : SkillBarAptitudeNegativeTex;
            }

            Widgets.FillableBar(cell.ContractedBy(0f, 1f), Mathf.Max(0.01f, level / 20f), fill, null, doBorder: false);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = disabled ? DisabledSkillColor : Color.white;
        Rect labelRect = new Rect(cell.x + 4f, cell.y, cell.width - LevelWidth - PassionSize - 8f, cell.height);
        Widgets.Label(labelRect, TextCache.Truncate(skill.def.skillLabel.CapitalizeFirst(), labelRect.width));

        if (!disabled && skill.passion > Passion.None)
        {
            Rect passionRect = new Rect(cell.xMax - LevelWidth - PassionSize - 2f, cell.y + (cell.height - PassionSize) / 2f, PassionSize, PassionSize);
            GUI.DrawTexture(passionRect, skill.passion == Passion.Major ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon);
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(cell.xMax - LevelWidth - 2f, cell.y, LevelWidth, cell.height), disabled ? "-" : level.ToStringCached());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        if (SkillDescription != null)
        {
            TooltipHandler.TipRegion(cell, new TipSignal(() => SkillDescription(skill), skill.def.GetHashCode() * 397945));
        }
    }

    // Current activity and weapon on one line; the tooltip has the base game's full inspect text.
    private static void DrawFooter(Pawn pawn, Rect rect)
    {
        string? activity = pawn.InMentalState
            ? pawn.MentalStateDef.LabelCap.Resolve()
            : pawn.jobs?.curDriver?.GetReport()?.CapitalizeFirst();
        string? weapon = pawn.equipment?.Primary?.LabelCap;

        Widgets.DrawHighlightIfMouseover(rect);
        Text.Font = GameFont.Small;
        Rect inner = new Rect(rect.x + 4f, rect.y, rect.width - 6f, rect.height);
        float weaponWidth = 0f;
        if (!weapon.NullOrEmpty())
        {
            // Weapon on the right, given up to 45% of the line; the activity takes what is left.
            string shownWeapon = TextCache.Truncate(weapon, inner.width * 0.45f);
            weaponWidth = Text.CalcSize(shownWeapon).x;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(inner, shownWeapon);
        }

        if (!activity.NullOrEmpty())
        {
            float activityWidth = inner.width - (weaponWidth > 0f ? weaponWidth + 12f : 0f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, activityWidth, inner.height), TextCache.Truncate(activity, activityWidth));
        }

        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(rect, new TipSignal(() => InspectText(pawn), 0x5C1A08));
    }

    private static Color TemperatureColor(PawnReadout.Snapshot snapshot)
    {
        if (!snapshot.Safe.Includes(snapshot.Temperature))
        {
            return ColorLibrary.RedReadable;
        }

        return snapshot.Comfortable.Includes(snapshot.Temperature) ? Color.white : WarningColor;
    }

    private static string ArmorTip(PawnReadout.Snapshot snapshot)
    {
        return "OverallArmor".Translate().Resolve().AsTipTitle() + "\n\n"
            + "ArmorSharp".Translate() + ": " + snapshot.ArmorSharp.ToStringPercent() + "\n"
            + "ArmorBlunt".Translate() + ": " + snapshot.ArmorBlunt.ToStringPercent() + "\n"
            + "ArmorHeat".Translate() + ": " + snapshot.ArmorHeat.ToStringPercent() + "\n\n"
            + "VUIP.PawnPaneArmorTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
    }

    private static string TemperatureTip(PawnReadout.Snapshot snapshot)
    {
        return "VUIP.PawnPaneTemperatureTip".Translate(
            snapshot.Temperature.ToStringTemperature("F1"),
            snapshot.Comfortable.min.ToStringTemperature("F0"), snapshot.Comfortable.max.ToStringTemperature("F0"),
            snapshot.Safe.min.ToStringTemperature("F0"), snapshot.Safe.max.ToStringTemperature("F0"));
    }

    private static string StatTip(Pawn pawn, StatDef stat, float value)
    {
        return stat.LabelCap.Resolve().AsTipTitle() + "\n\n" + stat.Worker.GetExplanationFull(StatRequest.For(pawn), stat.toStringNumberSense, value);
    }

    private static string NeedsTip(PawnReadout.Snapshot snapshot)
    {
        StringBuilder sb = new StringBuilder();
        foreach (Need need in snapshot.LowNeeds)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine().AppendLine();
            }

            sb.Append(need.GetTipString());
        }

        return sb.ToString();
    }

    private static string InspectText(Pawn pawn)
    {
        try
        {
            string text = pawn.GetInspectString();
            string lowPriority = pawn.GetInspectStringLowPriority();
            if (!lowPriority.NullOrEmpty())
            {
                text = text.NullOrEmpty() ? lowPriority : text.TrimEndNewlines() + "\n" + lowPriority;
            }

            return text;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static Action<WidgetRow, Pawn>? RowDrawer(string name)
    {
        return AccessTools.Method(typeof(InspectPaneFiller), name, new[] { typeof(WidgetRow), typeof(Pawn) }) is { } method
            ? AccessTools.MethodDelegate<Action<WidgetRow, Pawn>>(method)
            : null;
    }
}
