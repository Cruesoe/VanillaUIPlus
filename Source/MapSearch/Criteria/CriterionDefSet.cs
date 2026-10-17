using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaUIPlus;

/// <summary>Matches any of a set of defs that share a label, so same-named defs from different mods count as one.</summary>
public abstract class CriterionDefSet<T> : WorldCriterion
    where T : Def
{
    public List<string> defNames = new List<string>();
    private List<T>? resolved;

    protected CriterionDefSet()
    {
    }

    protected CriterionDefSet(IEnumerable<T> defs)
    {
        defNames = defs.Select(d => d.defName).ToList();
    }

    protected abstract string Title { get; }

    protected List<T> Defs => resolved ??= defNames.Select(n => DefDatabase<T>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

    public override string Label => Titled(Title, Defs.Count > 0 ? Defs[0].LabelCap.ToString() : defNames.FirstOrDefault() ?? "?");

    public override WorldCriterion Copy()
    {
        CriterionDefSet<T> copy = (CriterionDefSet<T>)MemberwiseClone();
        copy.defNames = new List<string>(defNames);
        return copy;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref defNames, "defs", LookMode.Value);
        defNames ??= new List<string>();
        resolved = null;
    }

    public static IEnumerable<IGrouping<string, T>> ByLabel(IEnumerable<T> defs)
    {
        return defs.GroupBy(d => d.LabelCap.ToString()).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);
    }
}
