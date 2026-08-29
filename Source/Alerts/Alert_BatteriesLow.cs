using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Warns before a power grid runs its batteries flat. Vanilla says nothing until the
/// lights actually go out, by which point coolers have already started thawing.
///
/// Only grids that are currently net-draining are considered: a grid pulling from its
/// batteries overnight while solar is down is normal, so the alert waits until that grid
/// is either low on charge or close to empty.
/// </summary>
public class Alert_BatteriesLow : Alert
{
    // A drain slower than this is treated as flat, both to avoid dividing by ~zero and
    // because a trickle that small will not empty anything in a meaningful time.
    private const float MinDrainPerTick = 1e-7f;

    private readonly List<Thing> batteries = new List<Thing>();
    private readonly List<string> lines = new List<string>();
    private readonly StringBuilder explanation = new StringBuilder();

    public Alert_BatteriesLow()
    {
        defaultLabel = "VUIP.BatteriesLow".Translate();
        defaultPriority = AlertPriority.High;
    }

    public override TaggedString GetExplanation()
    {
        explanation.Length = 0;
        foreach (string line in lines)
        {
            explanation.Append("  - ");
            explanation.AppendLine(line);
        }

        return "VUIP.BatteriesLowDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        if (!UiPlusMod.Settings.showBatteriesLowAlert)
        {
            return false;
        }

        Rebuild();
        return batteries.Count > 0 ? AlertReport.CulpritsAre(batteries) : AlertReport.Inactive;
    }

    private void Rebuild()
    {
        batteries.Clear();
        lines.Clear();
        float hoursLimit = UiPlusMod.Settings.batteryLowHours;
        float percentLimit = UiPlusMod.Settings.batteryLowPercent;
        List<Map> maps = Find.Maps;
        for (int m = 0; m < maps.Count; m++)
        {
            Map map = maps[m];
            if (!map.IsPlayerHome && map.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                continue;
            }

            List<PowerNet> nets = map.powerNetManager.AllNetsListForReading;
            for (int n = 0; n < nets.Count; n++)
            {
                Consider(nets[n], hoursLimit, percentLimit);
            }
        }
    }

    private void Consider(PowerNet net, float hoursLimit, float percentLimit)
    {
        List<CompPowerBattery> comps = net.batteryComps;
        if (comps == null || comps.Count == 0)
        {
            return;
        }

        float capacity = 0f;
        for (int i = 0; i < comps.Count; i++)
        {
            capacity += comps[i].Props.storedEnergyMax;
        }

        if (capacity <= 0f)
        {
            return;
        }

        // Only complain about a grid that is actually losing ground.
        float gainPerTick = net.CurrentEnergyGainRate();
        if (gainPerTick >= -MinDrainPerTick)
        {
            return;
        }

        float stored = net.CurrentStoredEnergy();
        float percent = stored / capacity * 100f;
        int ticksLeft = Mathf.RoundToInt(Mathf.Min(stored / -gainPerTick, GenDate.TicksPerYear));
        float hoursLeft = ticksLeft / (float)GenDate.TicksPerHour;
        if (percent > percentLimit && hoursLeft > hoursLimit)
        {
            return;
        }

        lines.Add("VUIP.BatteriesLowLine".Translate(
            percent.ToString("0"),
            ticksLeft.ToStringTicksToPeriod()));
        for (int i = 0; i < comps.Count; i++)
        {
            Thing parent = comps[i].parent;
            if (parent != null && !parent.Destroyed)
            {
                batteries.Add(parent);
            }
        }
    }
}
