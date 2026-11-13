using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Offers factions that mods added since the world default was saved, when the world page first opens with that default.
/// </summary>
public static class NewFactionOffer
{
    // Built on the world page's first open and opened on its first draw, so it stacks above the page.
    private static Dialog_NewFactions? pending;

    public static List<string> SelectableFactionNames()
    {
        return FactionGenerator.ConfigurableFactions
            .Where(f => f.displayInFactionSelection)
            .Select(f => f.defName)
            .Distinct()
            .ToList();
    }

    public static void Prepare(WorldDefaults defaults, List<FactionDef> gameFactions, List<FactionDef> factions, List<FactionDef> initialFactions)
    {
        pending = null;

        // A default saved before known factions were recorded can't tell new factions from ones left out on purpose.
        bool legacy = defaults.knownFactions == null;
        HashSet<string> known = new HashSet<string>(defaults.knownFactions ?? defaults.factions);
        List<FactionDef> offered = FactionGenerator.ConfigurableFactions
            .Where(f => f.displayInFactionSelection && !known.Contains(f.defName) && !factions.Contains(f))
            .Distinct()
            .ToList();

        if (offered.Count == 0)
        {
            if (legacy)
            {
                defaults.knownFactions = SelectableFactionNames();
                UiPlusMod.Instance.WriteSettings();
            }

            return;
        }

        pending = new Dialog_NewFactions(defaults, offered, gameFactions, factions, initialFactions, legacy);
    }

    public static void OpenPending()
    {
        if (pending != null)
        {
            Find.WindowStack.Add(pending);
            pending = null;
        }
    }
}
