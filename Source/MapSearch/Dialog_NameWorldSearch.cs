using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaUIPlus;

public class Dialog_NameWorldSearch : Dialog_NameEntry
{
    private readonly CriterionGroup query;

    public Dialog_NameWorldSearch(CriterionGroup query) : base(UniqueName())
    {
        this.query = query;
    }

    protected override string Prompt => "VUIP.WorldQueryNamePrompt".Translate();

    protected override string ControlName => "VUIP.WorldSearchName";

    private static string UniqueName()
    {
        List<SavedWorldSearch> saved = UiPlusMod.Settings.savedWorldSearches;
        for (int i = 1; ; i++)
        {
            string candidate = "VUIP.WorldQueryDefaultName".Translate(i);
            if (!saved.Any(s => s.name == candidate))
            {
                return candidate;
            }
        }
    }

    // Saving under an existing name replaces that search.
    protected override string AcceptLabel(string trimmedName)
    {
        bool replaces = UiPlusMod.Settings.savedWorldSearches.Any(s => s.name == trimmedName);
        return (replaces ? "VUIP.WorldQueryReplace" : "Save").Translate();
    }

    protected override void Accept(string trimmedName) => WorldQueryPanel.Save(trimmedName, query);
}
