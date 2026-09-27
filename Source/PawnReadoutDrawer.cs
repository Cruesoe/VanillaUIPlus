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

    // The activity and weapon labels are rebuilt this often rather than every frame.
    private const float FooterRefreshSeconds = 0.25f;

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

    // Reused for each row's cells; rows are drawn one at a time.
    private static readonly Rect[] CellBuffer = new Rect[3];
    private static readonly Dictionary<SkillDef, string> SkillLabels = new Dictionary<SkillDef, string>();

    private static Pawn? footerPawn;
    private static float footerTime = -1f;
    private static string? footerActivity;
    private static string? footerWeapon;

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
        if (Mouse.IsOver(button))
        {
            TooltipHandler.TipRegion(button, SelfTendTip(pawn, canDoctor, on));
        }

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

    // Same button and rules as the Bio tab's rename button (CharacterCardUtility): opens the name dialog, which also sets the title.
    public static void DrawRenameButton(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (!pawn.IsColonist && !pawn.IsColonySubhuman && !DebugSettings.ShowDevGizmos)
        {
            return;
        }

        Rect button = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
        lineEndWidth += 24f;
        TooltipHandler.TipRegionByKey(button, "RenameColonist");
        if (Widgets.ButtonImage(button.ContractedBy(2f), TexButton.Rename))
        {
            Find.WindowStack.Add(pawn.NamePawnDialog());
        }
    }

    private static string SelfTendTip(Pawn pawn, bool canDoctor, bool on)
    {
        string state = on ? TextCache.Key("VUIP.PawnPaneSelfTendOn") : TextCache.Key("VUIP.PawnPaneSelfTendOff");
        return "AllowSelfTend".Translate().CapitalizeFirst().Resolve().AsTipTitle() + ": " + state + "\n\n"
            + (canDoctor
                ? "AllowSelfTendTip".Translate(Faction.OfPlayer.def.pawnsPlural, 0.7f.ToStringPercent()).CapitalizeFirst().Resolve()
                : "MessageCannotSelfTendEver".Translate(pawn.LabelShort, pawn).Resolve());
    }

    // Age, gender and xenotype left of the header buttons, laid out right to left; tooltips and genes click match the Bio tab (CharacterCardUtility).
    public static void DrawIdentityIcons(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (pawn.ageTracker != null)
        {
            string age = pawn.ageTracker.AgeBiologicalYears.ToStringCached();
            float width = TextCache.Width(age) + 6f;
            Rect ageRect = new Rect(paneRect.width - lineEndWidth - width, 0f, width, 24f);
            lineEndWidth += width;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(ageRect, age);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(ageRect))
            {
                TooltipHandler.TipRegion(ageRect, () => pawn.ageTracker.AgeTooltipString, 0x5C1A10);
            }
        }

        if (pawn.gender != Gender.None)
        {
            Rect genderRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            GUI.DrawTexture(genderRect.ContractedBy(2f), pawn.gender.GetIcon());
            if (Mouse.IsOver(genderRect))
            {
                TooltipHandler.TipRegion(genderRect, () => pawn.gender.GetLabel(pawn.AnimalOrWildMan()).CapitalizeFirst(), 0x5C1A11);
            }
        }

        if (ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.GenesListForReading.Count > 0)
        {
            Rect xenoRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            if (Mouse.IsOver(xenoRect))
            {
                TooltipHandler.TipRegion(xenoRect, () => ("Xenotype".Translate() + ": " + pawn.genes.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor)
                    + "\n\n" + pawn.genes.XenotypeDescShort + "\n\n"
                    + "ViewGenesDesc".Translate(pawn.Named("PAWN")).ToString().StripTags().Colorize(ColoredText.SubtleGrayColor), 0x5C1A12);
            }

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
        UiPlusSettings settings = UiPlusMod.Settings;
        float y = rect.y;
        if (settings.pawnPaneShowArmor)
        {
            if (PawnReadout.CombatExtendedActive)
            {
                StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneArmor"), "-", SettingsWidgets.MutedColor, snapshot,
                    _ => TextCache.Key("VUIP.PawnPaneArmorCombatExtended"), 0x5C1A01);
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
            LabelStat(speedRow, TextCache.Key("VUIP.PawnPaneSpeed"));
            Rect[] cells = Cells(speedValues, 2);
            StatCell(cells[0], TextCache.Key("VUIP.PawnPaneMoveShort"), snapshot.MoveText, Color.white, snapshot.MoveSpeed / Mathf.Max(0.01f, snapshot.BaseMoveSpeed),
                snapshot, s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed), 0x5C1A03, centred: true);
            StatCell(cells[1], TextCache.Key("VUIP.PawnPaneWorkShort"), snapshot.WorkText, Color.white, snapshot.WorkSpeed,
                snapshot, s => StatTip(s.Pawn, StatDefOf.WorkSpeedGlobal, s.WorkSpeed), 0x5C1A09, centred: true);
        }

        if (settings.pawnPaneShowDps && NextRow(rect, ref y, out Rect combatRow, out Rect combatValues))
        {
            LabelStat(combatRow, snapshot.Ranged ? TextCache.Key("VUIP.PawnPaneRanged") : TextCache.Key("VUIP.PawnPaneMelee"));
            Color dpsColor = snapshot.Ranged && snapshot.Dps < 0f ? SettingsWidgets.MutedColor : Color.white;
            StatCell(combatValues, TextCache.Key("VUIP.PawnPaneDpsShort"), snapshot.DpsText, dpsColor, snapshot.HitChance, snapshot, CombatTip, 0x5C1A04);
        }

        if (snapshot.BleedRate > 0f)
        {
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneBleeding"), snapshot.BleedingText, ColorLibrary.RedReadable, snapshot, null, 0x5C1A06);
        }

        if (snapshot.LowNeeds.Count > 0)
        {
            StatRow(rect, ref y, TextCache.Key("VUIP.PawnPaneLowNeeds"), snapshot.LowNeedsText, snapshot.LowNeedsCritical ? ColorLibrary.RedReadable : WarningColor,
                snapshot, NeedsTip, 0x5C1A07);
        }
    }

    // Label on the left, value right-aligned like the skill levels; the tooltip covers the row.
    private static void StatRow(Rect area, ref float y, string label, string value, Color valueColor, PawnReadout.Snapshot snapshot,
        Func<PawnReadout.Snapshot, string>? tip, int tipId)
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
        Tip(row, snapshot, tip, tipId);
    }

    // Sharp, blunt and heat in three cells; bars fill against the 200% armor cap.
    private static void ArmorRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, TextCache.Key("VUIP.PawnPaneArmor"));
        Rect[] cells = Cells(valueRect, 3);
        StatCell(cells[0], TextCache.Key("VUIP.PawnPaneArmorSharpShort"), snapshot.ArmorSharpText, Color.white, snapshot.ArmorSharp / 2f,
            snapshot, s => TextCache.Key("ArmorSharp") + ": " + s.ArmorSharp.ToStringPercent(), 0x5C1A11);
        StatCell(cells[1], TextCache.Key("VUIP.PawnPaneArmorBluntShort"), snapshot.ArmorBluntText, Color.white, snapshot.ArmorBlunt / 2f,
            snapshot, s => TextCache.Key("ArmorBlunt") + ": " + s.ArmorBlunt.ToStringPercent(), 0x5C1A12);
        StatCell(cells[2], TextCache.Key("VUIP.PawnPaneArmorHeatShort"), snapshot.ArmorHeatText, Color.white, snapshot.ArmorHeat / 2f,
            snapshot, s => TextCache.Key("ArmorHeat") + ": " + s.ArmorHeat.ToStringPercent(), 0x5C1A13);
        Tip(new Rect(row.x, row.y, valueRect.x - row.x, row.height), snapshot, ArmorTip, 0x5C1A01);
    }

    // A thermometer scaled to this pawn's safe range plus a margin: red beyond safe, amber between safe and comfortable, green when comfortable, and a white tick (pinned to the edge when off the scale) for the temperature where the pawn stands.
    private static void ComfortRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, TextCache.Key("VUIP.PawnPaneComfort"));
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
        float here = Mathf.Clamp(strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, snapshot.Temperature), strip.x + 1f, strip.xMax - 1f);
        Widgets.DrawBoxSolid(new Rect(here - 1f, strip.y - 2f, 2f, strip.height + 3f), Color.white);

        // The range sits centred over the green comfortable zone, kept inside the value area.
        string range = TextCache.Truncate(snapshot.RangeText, valueRect.width - CellPad * 2f);
        float rangeWidth = TextCache.Width(range);
        float comfortCentre = strip.x + strip.width * Mathf.InverseLerp(scaleMin, scaleMax, (snapshot.Comfortable.min + snapshot.Comfortable.max) / 2f);
        float rangeX = Mathf.Clamp(comfortCentre - rangeWidth / 2f, valueRect.x + CellPad, valueRect.xMax - CellPad - rangeWidth);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = TemperatureColor(snapshot);
        Widgets.Label(new Rect(rangeX, valueRect.y, rangeWidth + 1f, valueRect.height - GaugeHeight), range);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Tip(row, snapshot, TemperatureTip, 0x5C1A02);
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

    // Splits a value area into equal cells with a small gap so their bars stay apart; the returned buffer is reused by the next row.
    private static Rect[] Cells(Rect area, int count)
    {
        float width = (area.width - CellGap * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            CellBuffer[i] = new Rect(area.x + i * (width + CellGap), area.y, width, area.height);
        }

        return CellBuffer;
    }

    // Grey caption on the left, never cut short; the value on the right; a thin gauge along the bottom.
    // With centred set, fill is the value divided by normal: the gauge grows right in green above normal (full at double) and left in red below (full at zero).
    private static void StatCell(Rect cell, string caption, string value, Color valueColor, float fill, PawnReadout.Snapshot snapshot,
        Func<PawnReadout.Snapshot, string> tip, int tipId, bool centred = false)
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
        float captionWidth = TextCache.Width(caption);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = LabelColor;
        Widgets.Label(new Rect(inner.x, inner.y, captionWidth + 1f, inner.height), caption);

        Rect valueRect = new Rect(inner.x + captionWidth + 3f, inner.y, Mathf.Max(0f, inner.width - captionWidth - 3f), inner.height);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = valueColor;
        Widgets.Label(valueRect, TextCache.Truncate(value, valueRect.width));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Tip(cell, snapshot, tip, tipId);
    }

    // Registers a tooltip only while the mouse is over the rect, so no closure is built on other frames.
    private static void Tip(Rect rect, PawnReadout.Snapshot snapshot, Func<PawnReadout.Snapshot, string>? tip, int tipId)
    {
        if (tip != null && Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, new TipSignal(() => tip(snapshot), tipId));
        }
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
                string[] labels =
                {
                    "VUIP.PawnPaneArmor", "VUIP.PawnPaneComfort", "VUIP.PawnPaneSpeed", "VUIP.PawnPaneMelee",
                    "VUIP.PawnPaneRanged", "VUIP.PawnPaneBleeding", "VUIP.PawnPaneLowNeeds"
                };
                foreach (string label in labels)
                {
                    statLabelWidth = Mathf.Max(statLabelWidth, TextCache.Width(TextCache.Key(label)));
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
                float armor = 3f * Mathf.Max(CellWidthFor("VUIP.PawnPaneArmorSharpShort", "188%"),
                    CellWidthFor("VUIP.PawnPaneArmorBluntShort", "188%"),
                    CellWidthFor("VUIP.PawnPaneArmorHeatShort", "188%")) + CellGap * 2f;
                float speed = 2f * Mathf.Max(CellWidthFor("VUIP.PawnPaneMoveShort", "8.88"),
                    CellWidthFor("VUIP.PawnPaneWorkShort", "188%")) + CellGap;
                float combat = CellWidthFor("VUIP.PawnPaneDpsShort", "88.8");
                float comfort = TextCache.Width(PawnReadout.TemperatureRange(-99f, 99f)) + CellPad;
                minStatsWidth = StatLabelWidth + Mathf.Max(armor, speed, combat, comfort) + 2f;
            }

            return minStatsWidth;
        }
    }

    // Pane width that fits the stats column next to the skills: stats, divider gap and skills plus the pane's 12px margins.
    public static float MinPaneWidth(bool withSkills)
    {
        return MinStatsWidth + (withSkills ? ColumnGap + SkillsWidth : 0f) + 24f;
    }

    private static float CellWidthFor(string captionKey, string widestValue)
    {
        return CellPad * 2f + TextCache.Width(TextCache.Key(captionKey)) + 3f + TextCache.Width(widestValue) + CellSlack;
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
            Widgets.Label(rect, TextCache.Key("SkillsDevelopLaterBaby"));
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
        Widgets.Label(labelRect, TextCache.Truncate(SkillLabel(skill.def), labelRect.width));

        if (!disabled && skill.passion > Passion.None)
        {
            Rect passionRect = new Rect(cell.xMax - LevelWidth - PassionSize - 2f, cell.y + (cell.height - PassionSize) / 2f, PassionSize, PassionSize);
            GUI.DrawTexture(passionRect, skill.passion == Passion.Major ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon);
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(cell.xMax - LevelWidth - 2f, cell.y, LevelWidth, cell.height), disabled ? "-" : level.ToStringCached());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        if (SkillDescription != null && Mouse.IsOver(cell))
        {
            TooltipHandler.TipRegion(cell, new TipSignal(() => SkillDescription(skill), skill.def.GetHashCode() * 397945));
        }
    }

    private static string SkillLabel(SkillDef def)
    {
        if (!SkillLabels.TryGetValue(def, out string label))
        {
            label = def.skillLabel.CapitalizeFirst();
            SkillLabels[def] = label;
        }

        return label;
    }

    // Current activity and weapon on one line; the tooltip has the base game's full inspect text.
    private static void DrawFooter(Pawn pawn, Rect rect)
    {
        RefreshFooter(pawn);
        Widgets.DrawHighlightIfMouseover(rect);
        Text.Font = GameFont.Small;
        Rect inner = new Rect(rect.x + 4f, rect.y, rect.width - 6f, rect.height);
        float weaponWidth = 0f;
        if (!footerWeapon.NullOrEmpty())
        {
            // Weapon on the right, given up to 45% of the line; the activity takes what is left.
            string shownWeapon = TextCache.Truncate(footerWeapon, inner.width * 0.45f);
            weaponWidth = TextCache.Width(shownWeapon);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(inner, shownWeapon);
        }

        if (!footerActivity.NullOrEmpty())
        {
            float activityWidth = inner.width - (weaponWidth > 0f ? weaponWidth + 12f : 0f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, activityWidth, inner.height), TextCache.Truncate(footerActivity, activityWidth));
        }

        Text.Anchor = TextAnchor.UpperLeft;
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, new TipSignal(() => InspectText(pawn), 0x5C1A08));
        }
    }

    private static void RefreshFooter(Pawn pawn)
    {
        float now = Time.realtimeSinceStartup;
        if (pawn == footerPawn && now - footerTime < FooterRefreshSeconds && now >= footerTime)
        {
            return;
        }

        footerPawn = pawn;
        footerTime = now;
        footerActivity = pawn.InMentalState
            ? pawn.MentalStateDef.LabelCap.Resolve()
            : pawn.jobs?.curDriver?.GetReport()?.CapitalizeFirst();
        footerWeapon = pawn.equipment?.Primary?.LabelCap;
    }

    private static Color TemperatureColor(PawnReadout.Snapshot snapshot)
    {
        if (!snapshot.Safe.Includes(snapshot.Temperature))
        {
            return ColorLibrary.RedReadable;
        }

        return snapshot.Comfortable.Includes(snapshot.Temperature) ? Color.white : WarningColor;
    }

    private static string CombatTip(PawnReadout.Snapshot snapshot)
    {
        if (!snapshot.Ranged)
        {
            return StatTip(snapshot.Pawn, StatDefOf.MeleeDPS, snapshot.Dps) + "\n\n" + StatTip(snapshot.Pawn, StatDefOf.MeleeHitChance, snapshot.HitChance);
        }

        string dps = snapshot.RangedTip ?? (PawnReadout.CombatExtendedActive
            ? TextCache.Key("VUIP.PawnPaneRangedDpsCombatExtended")
            : TextCache.Key("VUIP.PawnPaneRangedDpsUnknown"));
        return dps + "\n\n" + snapshot.HitTip;
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
