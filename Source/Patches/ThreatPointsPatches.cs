using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Every incident sends its letter through here with the parms that carry its final points.
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.SendIncidentLetter))]
public static class Patch_IncidentWorker_SendIncidentLetter_ThreatPoints
{
    public static void Prefix(IncidentParms parms, IncidentDef def)
    {
        ThreatPoints.Begin(def, parms);
    }

    // Also runs when no letter was received, so the incident isn't carried over to a later letter.
    public static Exception? Finalizer(Exception? __exception)
    {
        ThreatPoints.End();
        return __exception;
    }
}

[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), typeof(Letter), typeof(string), typeof(int), typeof(bool))]
public static class Patch_LetterStack_ReceiveLetter_ThreatPoints
{
    public static void Prefix(Letter let)
    {
        ThreatPoints.Apply(let);
    }
}
