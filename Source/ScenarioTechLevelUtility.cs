using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class ScenarioTechLevelUtility
{
    private static readonly AccessTools.FieldRef<Scenario, ScenPart_PlayerFaction?>? PlayerFactionRef =
        ReflectionGuard.FieldRef<Scenario, ScenPart_PlayerFaction?>(nameof(Scenario), "playerFaction", AccessTools.Field(typeof(Scenario), "playerFaction"));
    private static readonly AccessTools.FieldRef<ScenPart_PlayerFaction, FactionDef?>? FactionDefRef =
        ReflectionGuard.FieldRef<ScenPart_PlayerFaction, FactionDef?>(nameof(ScenPart_PlayerFaction), "factionDef", AccessTools.Field(typeof(ScenPart_PlayerFaction), "factionDef"));

    public static FactionDef? FactionDefOf(Scenario scen)
    {
        if (PlayerFactionRef == null || FactionDefRef == null)
        {
            return null;
        }

        ScenPart_PlayerFaction? playerFaction = PlayerFactionRef(scen);
        return playerFaction != null ? FactionDefRef(playerFaction) : null;
    }

    public static TechLevel TechLevelOf(Scenario scen) => FactionDefOf(scen)?.techLevel ?? TechLevel.Undefined;

    // Matches the palette used by Semi Random Research P-Fork Continued's research tree tech-level coloring.
    public static Color ColorFor(TechLevel techLevel)
    {
        switch (techLevel)
        {
            case TechLevel.Animal:
                return new Color(0.5f, 0.4f, 0.2f);
            case TechLevel.Neolithic:
                return new Color(0.6f, 0.35f, 0.35f);
            case TechLevel.Medieval:
                return new Color(0.6f, 0.6f, 0.3f);
            case TechLevel.Industrial:
                return new Color(0.4f, 0.6f, 0.3f);
            case TechLevel.Spacer:
                return new Color(0.3f, 0.5f, 0.6f);
            case TechLevel.Ultra:
                return new Color(0.45f, 0.35f, 0.6f);
            case TechLevel.Archotech:
                return new Color(0.6f, 0.35f, 0.6f);
            default:
                return TexUI.AvailResearchColor;
        }
    }
}
