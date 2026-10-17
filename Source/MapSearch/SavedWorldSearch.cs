using Verse;

namespace VanillaUIPlus;

public class SavedWorldSearch : IExposable
{
    public string name = "";
    public CriterionGroup root = new CriterionGroup(CriterionGroupMode.All);

    public void ExposeData()
    {
        Scribe_Values.Look(ref name, "name", "");
        Scribe_Deep.Look(ref root, "root");
        root ??= new CriterionGroup(CriterionGroupMode.All);
        root.mode = CriterionGroupMode.All;
    }
}
