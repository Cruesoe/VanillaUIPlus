using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Extra sections for the pawn pane's skill tooltips: the stats each skill affects, and Vanilla Skills Expanded expertise (through reflection).
/// </summary>
public static class SkillTooltip
{
    public const string SkillsExpandedPackageId = "vanillaexpanded.skills";

    // Stats whose skillNeedFactors or skillNeedOffsets name the skill, so modded skill stats are included.
    private static Dictionary<SkillDef, List<StatDef>>? statsBySkill;
    private static int statDefCount = -1;

    private static bool expertiseResolved;
    private static Func<Pawn, object?>? expertiseTracker;
    private static PropertyInfo? allExpertise;
    private static FieldInfo? recordDef;
    private static PropertyInfo? recordLevel;
    private static FieldInfo? expertiseSkill;
    private static MethodInfo? expertiseEffects;

    /// <summary>Whether Vanilla Skills Expanded is active.</summary>
    public static bool SkillsExpandedActive => ModsConfig.IsActive(SkillsExpandedPackageId);

    public static string Append(string baseText, SkillRecord skill)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        StringBuilder builder = new StringBuilder(baseText.TrimEnd());
        if (settings.pawnPaneSkillExpertise && SkillsExpandedActive)
        {
            AppendExpertise(builder, skill);
        }

        if (settings.pawnPaneSkillStats && !skill.TotallyDisabled)
        {
            AppendStats(builder, skill);
        }

        return builder.ToString();
    }

    private static void AppendStats(StringBuilder builder, SkillRecord skill)
    {
        List<StatDef> stats = StatsFor(skill.def);
        Pawn pawn = skill.Pawn;
        bool header = false;
        for (int i = 0; i < stats.Count; i++)
        {
            StatDef stat = stats[i];
            if (!stat.Worker.ShouldShowFor(StatRequest.For(pawn)) || stat.Worker.IsDisabledFor(pawn))
            {
                continue;
            }

            if (!header)
            {
                builder.AppendLine().AppendLine();
                builder.AppendLineTagged(TextCache.Key("VUIP.SkillTipStats").AsTipTitle());
                header = true;
            }

            builder.AppendLine($"  - {stat.LabelCap}: {stat.ValueToString(pawn.GetStatValue(stat))}");
        }
    }

    private static List<StatDef> StatsFor(SkillDef skill)
    {
        List<StatDef> all = DefDatabase<StatDef>.AllDefsListForReading;
        if (statsBySkill == null || statDefCount != all.Count)
        {
            statDefCount = all.Count;
            statsBySkill = new Dictionary<SkillDef, List<StatDef>>();
            foreach (StatDef stat in all)
            {
                AddNeeds(stat, stat.skillNeedFactors);
                AddNeeds(stat, stat.skillNeedOffsets);
            }

            foreach (List<StatDef> list in statsBySkill.Values)
            {
                list.SortBy(stat => stat.LabelCap.RawText);
            }
        }

        return statsBySkill.TryGetValue(skill, out List<StatDef>? stats) ? stats : new List<StatDef>();
    }

    private static void AddNeeds(StatDef stat, List<SkillNeed>? needs)
    {
        if (needs == null)
        {
            return;
        }

        foreach (SkillNeed need in needs)
        {
            if (need?.skill == null)
            {
                continue;
            }

            if (!statsBySkill!.TryGetValue(need.skill, out List<StatDef>? list))
            {
                list = new List<StatDef>();
                statsBySkill[need.skill] = list;
            }

            if (!list.Contains(stat))
            {
                list.Add(stat);
            }
        }
    }

    // Each expertise for this skill with its level and effects, as Vanilla Skills Expanded's own expertise tooltip words them.
    private static void AppendExpertise(StringBuilder builder, SkillRecord skill)
    {
        ResolveExpertise();
        if (expertiseTracker == null || allExpertise == null || recordDef == null || recordLevel == null || expertiseSkill == null || expertiseEffects == null)
        {
            return;
        }

        try
        {
            if (expertiseTracker(skill.Pawn) is not { } tracker || allExpertise.GetValue(tracker) is not IList records)
            {
                return;
            }

            bool header = false;
            foreach (object record in records)
            {
                object? def = record == null ? null : recordDef.GetValue(record);
                if (def is not Def expertise || expertiseSkill.GetValue(def) != skill.def)
                {
                    continue;
                }

                if (!header)
                {
                    builder.AppendLine().AppendLine();
                    builder.AppendLineTagged(TextCache.Key("VUIP.SkillTipExpertise").AsTipTitle());
                    header = true;
                }

                int level = recordLevel.GetValue(record) is int value ? value : 0;
                builder.Append($"  {expertise.LabelCap}: {"Level".Translate().CapitalizeFirst()} {level}");
                builder.AppendLine(expertiseEffects.Invoke(def, new object[] { level, "    - " }) as string ?? "");
            }
        }
        catch (Exception exception)
        {
            Log.WarningOnce($"[Vanilla UI+] Could not read Vanilla Skills Expanded expertise; it is left out of skill tooltips.\n{exception}", 0x5C1A40);
            expertiseTracker = null;
        }
    }

    private static void ResolveExpertise()
    {
        if (expertiseResolved)
        {
            return;
        }

        expertiseResolved = true;
        Type? trackers = AccessTools.TypeByName("VSE.ExpertiseTrackers");
        MethodInfo? get = trackers == null ? null : AccessTools.Method(trackers, "Expertise", new[] { typeof(Pawn) });
        if (!ReflectionGuard.Found("ExpertiseTrackers", "Expertise(Pawn)", get))
        {
            return;
        }

        allExpertise = AccessTools.Property(get!.ReturnType, "AllExpertise");
        Type? record = AccessTools.TypeByName("VSE.ExpertiseRecord");
        recordDef = record == null ? null : AccessTools.Field(record, "def");
        recordLevel = record == null ? null : AccessTools.Property(record, "Level");
        Type? def = AccessTools.TypeByName("VSE.Expertise.ExpertiseDef");
        expertiseSkill = def == null ? null : AccessTools.Field(def, "skill");
        expertiseEffects = def == null ? null : AccessTools.Method(def, "Effects", new[] { typeof(int), typeof(string) });
        if (ReflectionGuard.Found("ExpertiseTracker", "AllExpertise", allExpertise)
            && ReflectionGuard.Found("ExpertiseRecord", "def/Level", recordDef != null ? recordLevel : null)
            && ReflectionGuard.Found("ExpertiseDef", "skill/Effects", expertiseSkill != null ? expertiseEffects : null))
        {
            expertiseTracker = pawn => get.Invoke(null, new object[] { pawn });
        }
    }
}
