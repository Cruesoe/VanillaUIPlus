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
public static partial class PawnReadoutDrawer
{
    private const float SkillsWidth = 272f;
    private const float ColumnGap = 12f;
    private const float SkillGap = 8f;

    private const float LevelWidth = 22f;
    private const float PassionSize = 14f;


    // The activity and weapon labels are rebuilt this often rather than every frame.
    private const float FooterRefreshSeconds = 0.25f;

    private static readonly Color WarningColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color LabelColor = SettingsWidgets.MutedColor;

    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color DisabledSkillColor = new Color(1f, 1f, 1f, 0.5f);

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
            Rect body = new Rect(0f, PawnReadout.TopBarHeight + PawnReadout.SectionGap, rect.width, PawnReadout.BodyHeight);
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
            Text.WordWrap = true;
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
        float rowHeight = rect.height / Mathf.Max(PawnReadout.StatRows, PawnReadout.SkillRows);
        Widgets.DrawBoxSolid(new Rect(rect.x + cellWidth + SkillGap / 2f, rect.y, 1f, rect.height), DividerColor);
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord? skill = pawn.skills.GetSkill(skills[i]);
            if (skill == null)
            {
                continue;
            }

            Rect cell = new Rect(rect.x + (i % 2) * (cellWidth + SkillGap), rect.y + (i / 2) * rowHeight, cellWidth, rowHeight);
            if (cell.yMax > rect.yMax + 0.5f)
            {
                break;
            }

            DrawSkill(skill, cell);
        }
    }

    // Skill rows keep the Bio tab's passion icons and tooltips, with aptitude indicated at the left edge.
    private static void DrawSkill(SkillRecord skill, Rect cell)
    {
        Widgets.DrawHighlightIfMouseover(cell);
        Widgets.DrawBoxSolid(new Rect(cell.x, cell.yMax - 1f, cell.width, 1f), DividerColor * new Color(1f, 1f, 1f, 0.65f));
        int level = skill.GetLevel();
        bool disabled = skill.TotallyDisabled;
        if (!disabled && (ModsConfig.BiotechActive || ModsConfig.AnomalyActive) && skill.Aptitude != 0)
        {
            Widgets.DrawBoxSolid(new Rect(cell.x, cell.y + 5f, 2f, cell.height - 10f),
                skill.Aptitude > 0 ? TemperatureGreen : TemperatureRed);
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
        EnsureLanguage();
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

    // Drops the footer's cached pawn so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        footerPawn = null;
        footerActivity = null;
        footerWeapon = null;
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
