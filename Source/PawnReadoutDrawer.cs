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

    private static readonly Color WarningColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color DisabledSkillColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Texture2D SkillBarFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 1f, 1f, 0.1f));
    private static readonly Texture2D SkillBarAptitudePositiveTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 1f, 0.6f, 0.25f));
    private static readonly Texture2D SkillBarAptitudeNegativeTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.5f, 0.6f, 0.25f));

    // Private base game drawers for the bar row, so it behaves exactly as in the base game (the area bar opens its menu).
    private static readonly Action<WidgetRow, Pawn>? DrawMood = RowDrawer("DrawMood");
    private static readonly Action<WidgetRow, Pawn>? DrawTimetableSetting = RowDrawer("DrawTimetableSetting");
    private static readonly Action<WidgetRow, Pawn>? DrawAreaAllowed = RowDrawer("DrawAreaAllowed");
    private static readonly Func<SkillRecord, string>? SkillDescription =
        AccessTools.Method(typeof(SkillUI), "GetSkillDescription") is { } method
            ? AccessTools.MethodDelegate<Func<SkillRecord, string>>(method)
            : null;

    private static readonly Texture2D SelfTendTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/SelfTend");
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
            GUI.DrawTexture(button.ContractedBy(2f), SelfTendTex);
            GUI.color = Color.white;
            return;
        }

        Color color = on ? Color.white : SelfTendOffColor;
        if (Widgets.ButtonImage(button.ContractedBy(2f), SelfTendTex, color, on ? GenUI.MouseoverColor : Color.white))
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
        InspectPaneFiller.DrawHealth(row, pawn);
        DrawMood?.Invoke(row, pawn);
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
            string range = snapshot.Comfortable.min.ToStringTemperature("F0") + " ~ " + snapshot.Comfortable.max.ToStringTemperature("F0");
            StatRow(rect, ref y, "VUIP.PawnPaneComfort".Translate(), range, TemperatureColor(snapshot),
                () => TemperatureTip(snapshot), 0x5C1A02);
        }

        if (settings.pawnPaneShowSpeed)
        {
            StatDef stat = StatDefOf.MoveSpeed;
            StatRow(rect, ref y, stat.LabelCap, stat.ValueToString(snapshot.MoveSpeed), Color.white,
                () => StatTip(pawn, stat, snapshot.MoveSpeed), 0x5C1A03);
        }

        if (settings.pawnPaneShowDps)
        {
            if (snapshot.Ranged)
            {
                string value = snapshot.Dps >= 0f ? snapshot.Dps.ToString("0.0") : "-";
                string tip = snapshot.RangedTip ?? (PawnReadout.CombatExtendedActive
                    ? "VUIP.PawnPaneRangedDpsCombatExtended".Translate().Resolve()
                    : "VUIP.PawnPaneRangedDpsUnknown".Translate().Resolve());
                StatRow(rect, ref y, "VUIP.PawnPaneRangedDps".Translate(), value, snapshot.Dps >= 0f ? Color.white : SettingsWidgets.MutedColor,
                    () => tip, 0x5C1A04);
            }
            else
            {
                StatDef stat = StatDefOf.MeleeDPS;
                StatRow(rect, ref y, "VUIP.PawnPaneMeleeDps".Translate(), stat.ValueToString(snapshot.Dps), Color.white,
                    () => StatTip(pawn, stat, snapshot.Dps), 0x5C1A05);
            }
        }

        if (snapshot.BleedRate > 0f)
        {
            string bleeding = snapshot.TicksToBleedOut > 0 && snapshot.TicksToBleedOut < int.MaxValue
                ? "VUIP.PawnPaneBleedingValue".Translate(snapshot.BleedRate.ToStringPercent(), snapshot.TicksToBleedOut.ToStringTicksToPeriod())
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

    // Sharp, blunt and heat in three right-aligned cells, each with a muted initial and its own tooltip.
    private static void ArmorRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        if (!NextRow(area, ref y, out Rect row, out Rect valueRect))
        {
            return;
        }

        LabelStat(row, "VUIP.PawnPaneArmor".Translate());
        float cellWidth = valueRect.width / 3f;
        ArmorCell(new Rect(valueRect.x, row.y, cellWidth, row.height), "VUIP.PawnPaneArmorSharpShort".Translate(), "ArmorSharp".Translate(), snapshot.ArmorSharp, 0x5C1A11);
        ArmorCell(new Rect(valueRect.x + cellWidth, row.y, cellWidth, row.height), "VUIP.PawnPaneArmorBluntShort".Translate(), "ArmorBlunt".Translate(), snapshot.ArmorBlunt, 0x5C1A12);
        ArmorCell(new Rect(valueRect.x + cellWidth * 2f, row.y, cellWidth, row.height), "VUIP.PawnPaneArmorHeatShort".Translate(), "ArmorHeat".Translate(), snapshot.ArmorHeat, 0x5C1A13);
        TooltipHandler.TipRegion(new Rect(row.x, row.y, valueRect.x - row.x, row.height), new TipSignal(() => ArmorTip(snapshot), 0x5C1A01));
    }

    private static void ArmorCell(Rect cell, string initial, string name, float value, int tipId)
    {
        string percent = value.ToStringPercent("F0");
        float percentWidth = Text.CalcSize(percent).x;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(cell, percent);
        GUI.color = SettingsWidgets.MutedColor;
        Widgets.Label(new Rect(cell.x, cell.y, cell.width - percentWidth - 3f, cell.height), initial);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(cell, new TipSignal(name + ": " + value.ToStringPercent(), tipId));
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
                    "VUIP.PawnPaneArmor".Translate(), "VUIP.PawnPaneComfort".Translate(), StatDefOf.MoveSpeed.LabelCap,
                    "VUIP.PawnPaneMeleeDps".Translate(), "VUIP.PawnPaneRangedDps".Translate(), "VUIP.PawnPaneBleeding".Translate(),
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
        Widgets.Label(new Rect(row.x + 4f, row.y, labelWidth - 4f, row.height), TextCache.Truncate(label, labelWidth - 4f));
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
