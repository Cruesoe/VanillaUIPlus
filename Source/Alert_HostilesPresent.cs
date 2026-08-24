using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaUIPlus.Alerts;

public class Alert_HostilesPresent : Alert_Critical
{
    private static readonly Color DarkRed = new Color(0.42f, 0.08f, 0.08f, 0.85f);

    private readonly List<Thing> hostiles = new List<Thing>();
    private readonly StringBuilder explanation = new StringBuilder();

    public Alert_HostilesPresent()
    {
        defaultLabel = "VUIA.HostilesPresent".Translate();
    }

    protected override bool DoMessage => false;

    protected override Color BGColor => DarkRed;

    private List<Thing> Hostiles
    {
        get
        {
            hostiles.Clear();
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (!MapHasPlayerPresence(map))
                {
                    continue;
                }

                foreach (IAttackTarget target in map.attackTargetsCache.TargetsHostileToColony)
                {
                    if (!GenHostility.IsActiveThreatToPlayer(target, canBeFogged: true))
                    {
                        continue;
                    }

                    Thing thing = target.Thing;
                    if (thing != null && !thing.Destroyed)
                    {
                        hostiles.Add(thing);
                    }
                }
            }

            return hostiles;
        }
    }

    public override TaggedString GetExplanation()
    {
        explanation.Length = 0;
        foreach (Thing thing in hostiles)
        {
            explanation.Append("  - ");
            explanation.AppendLine(thing.LabelCap);
        }

        return "VUIA.HostilesPresentDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        if (!AlertsMod.Enabled)
        {
            return false;
        }

        return AlertReport.CulpritsAre(Hostiles);
    }

    private static bool MapHasPlayerPresence(Map map)
    {
        if (map.IsPlayerHome)
        {
            return true;
        }

        return map.mapPawns.FreeColonistsSpawnedCount > 0;
    }
}
