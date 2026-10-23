using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>Everything on the Assign tab as pinned from one pawn, saved with the mod settings.</summary>
public class AssignDefaults : IExposable
{
    public HostilityResponseMode? hostilityResponse;
    public MedicalCareCategory? medicalCare;

    // Matched by name; a colony without a policy of that name keeps its default.
    public string? apparelPolicy;
    public string? foodPolicy;
    public string? drugPolicy;
    public string? readingPolicy;

    // Every inventory stock group by defName, including groups other mods add.
    public Dictionary<string, string> stockThings = new Dictionary<string, string>();
    public Dictionary<string, int> stockCounts = new Dictionary<string, int>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref hostilityResponse, "hostilityResponse");
        Scribe_Values.Look(ref medicalCare, "medicalCare");
        Scribe_Values.Look(ref apparelPolicy, "apparelPolicy");
        Scribe_Values.Look(ref foodPolicy, "foodPolicy");
        Scribe_Values.Look(ref drugPolicy, "drugPolicy");
        Scribe_Values.Look(ref readingPolicy, "readingPolicy");
        Scribe_Collections.Look(ref stockThings, "stockThings", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref stockCounts, "stockCounts", LookMode.Value, LookMode.Value);
        stockThings ??= new Dictionary<string, string>();
        stockCounts ??= new Dictionary<string, int>();
    }
}
