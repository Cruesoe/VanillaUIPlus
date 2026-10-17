using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public enum FactionMatch
{
    Any,
    NotPlayer,
    NotHostile,
    Hostile,
    NotPermanentEnemy,
    PermanentEnemy,
    Specific
}

/// <summary>Picks which factions' settlements count. A specific faction missing from the current game counts as any.</summary>
public static class FactionFilter
{
    public static bool Matches(Faction? faction, FactionMatch match, int factionId)
    {
        if (faction == null)
        {
            return false;
        }

        Faction player = Faction.OfPlayer;
        return match switch
        {
            FactionMatch.NotPlayer => !faction.IsPlayer,
            FactionMatch.NotHostile => !faction.HostileTo(player),
            FactionMatch.Hostile => faction.HostileTo(player),
            FactionMatch.NotPermanentEnemy => !faction.def.PermanentlyHostileTo(player.def),
            FactionMatch.PermanentEnemy => faction.def.PermanentlyHostileTo(player.def),
            FactionMatch.Specific => Specific(factionId) is not { } specific || faction == specific,
            _ => true
        };
    }

    public static string Label(FactionMatch match, int factionId)
    {
        return match switch
        {
            FactionMatch.NotPlayer => "VUIP.WorldQueryFactionNotPlayer".Translate(),
            FactionMatch.NotHostile => "VUIP.WorldQueryFactionNotHostile".Translate(),
            FactionMatch.Hostile => "VUIP.WorldQueryFactionHostile".Translate(),
            FactionMatch.NotPermanentEnemy => "VUIP.WorldQueryFactionNotPermanentEnemy".Translate(),
            FactionMatch.PermanentEnemy => "VUIP.WorldQueryFactionPermanentEnemy".Translate(),
            FactionMatch.Specific when Specific(factionId) is { } faction => faction.Name,
            _ => "VUIP.WorldQueryFactionAny".Translate()
        };
    }

    public static void Draw(Rect rect, FactionMatch match, int factionId, Action<FactionMatch, int> set)
    {
        Rect button = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
        if (!Widgets.ButtonText(button, Label(match, factionId).Truncate(button.width - 12f)))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (FactionMatch kind in Enum.GetValues(typeof(FactionMatch)))
        {
            if (kind != FactionMatch.Specific)
            {
                FactionMatch chosen = kind;
                options.Add(new FloatMenuOption(Label(kind, -1), () => set(chosen, -1)));
            }
        }

        if (Find.FactionManager != null)
        {
            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                int id = faction.loadID;
                options.Add(new FloatMenuOption(faction.Name, () => set(FactionMatch.Specific, id), faction.def.FactionIcon, faction.Color));
            }
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static Faction? Specific(int factionId)
    {
        return factionId < 0 ? null : Find.FactionManager?.AllFactions.FirstOrDefault(f => f.loadID == factionId);
    }
}
