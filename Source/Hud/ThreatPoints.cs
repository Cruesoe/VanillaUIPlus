using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Adds the threat points an incident used to the letter it sends: the points are taken from the incident's parms when its letter is sent and written into the letter, so they are saved with it.
/// </summary>
public static class ThreatPoints
{
    private const string VisibleRaidPointsPackageId = "visibleraidpoints.1trickpwnyta";

    private static bool? otherModActive;
    private static IncidentDef? pendingDef;
    private static IncidentParms? pendingParms;

    /// <summary>Visible Raid Points is active, so it shows the points and this stays out of its way.</summary>
    public static bool OtherModActive => otherModActive ??= ModLister.GetActiveModWithIdentifier(VisibleRaidPointsPackageId, ignorePostfix: true) != null;

    // Remembers the incident whose letter is about to be received.
    public static void Begin(IncidentDef? def, IncidentParms? parms)
    {
        pendingDef = def;
        pendingParms = parms;
    }

    public static void End()
    {
        pendingDef = null;
        pendingParms = null;
    }

    // Writes the pending incident's points into the first letter received after Begin.
    public static void Apply(Letter letter)
    {
        IncidentDef? def = pendingDef;
        IncidentParms? parms = pendingParms;
        End();
        UiPlusSettings settings = UiPlusMod.Settings;
        if (def == null || parms == null || letter is not ChoiceLetter choice || OtherModActive || parms.points <= 0f
            || !(settings.threatPointsInText || settings.threatPointsInLabel) || !Shown(def, settings))
        {
            return;
        }

        int points = Mathf.RoundToInt(parms.points);
        if (settings.threatPointsInLabel)
        {
            choice.Label = "(" + points + ") " + choice.Label;
        }

        if (settings.threatPointsInText)
        {
            choice.Text = choice.Text + "\n\n" + "VUIP.ThreatPoints".Translate(points);
        }
    }

    // Hostile threats, and friendly raids when asked for.
    private static bool Shown(IncidentDef def, UiPlusSettings settings)
    {
        if (def.Worker is IncidentWorker_RaidFriendly)
        {
            return settings.threatPointsFriendly;
        }

        return def.category == IncidentCategoryDefOf.ThreatBig || def.category == IncidentCategoryDefOf.ThreatSmall;
    }
}
