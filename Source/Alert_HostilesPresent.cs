using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaUIPlus;

public class Alert_HostilesPresent : Alert_Critical
{
    private const int RecalcFrameInterval = 20;
    private static readonly Color DarkRed = new Color(0.42f, 0.08f, 0.08f, 0.85f);

    private readonly List<Thing> hostiles = new List<Thing>();
    private readonly StringBuilder explanation = new StringBuilder();
    private static int lastRecalcFrame = -1;

    public static Alert_HostilesPresent? Instance { get; private set; }

    public Alert_HostilesPresent()
    {
        Instance = this;
        defaultLabel = "VUIP.HostilesPresent".Translate();
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

        return "VUIP.HostilesPresentDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        // Deliberately not gated on UiPlusMod.Enabled: that toggle governs custom HUD
        // drawing only. With the HUD left vanilla this alert is not pinned, so it falls
        // back to the normal alert stack instead of disappearing.
        if (!UiPlusMod.Settings.showHostilesPresentAlert)
        {
            return false;
        }

        return AlertReport.CulpritsAre(Hostiles);
    }

    public static void DrawPinned(ref float curBaseY)
    {
        if (!UiPlusMod.Enabled || !UiPlusMod.Settings.showHostilesPresentAlert || Instance == null)
        {
            return;
        }

        if (lastRecalcFrame < 0 || Time.frameCount - lastRecalcFrame >= RecalcFrameInterval)
        {
            lastRecalcFrame = Time.frameCount;
            Instance.Recalculate();
        }

        if (!Instance.Active || SnoozeTracker.IsSnoozed(Instance))
        {
            return;
        }

        float height = AlertDrawer.HeightFor(Instance);
        AlertDrawer.DrawAt(Instance, curBaseY - height);
        curBaseY -= height;
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
