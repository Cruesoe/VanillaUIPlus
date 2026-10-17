using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public enum CriterionGroupMode
{
    All,
    Any,
    None
}

/// <summary>Combines other criteria. An empty group matches every tile.</summary>
public class CriterionGroup : WorldCriterion
{
    public CriterionGroupMode mode;
    public List<WorldCriterion> children = new List<WorldCriterion>();

    public CriterionGroup()
    {
    }

    public CriterionGroup(CriterionGroupMode mode)
    {
        this.mode = mode;
    }

    public override string Label => mode switch
    {
        CriterionGroupMode.Any => "VUIP.WorldQueryAnyOf".Translate(),
        CriterionGroupMode.None => "VUIP.WorldQueryNoneOf".Translate(),
        _ => "VUIP.WorldQueryAllOf".Translate()
    };

    public override bool Matches(Tile tile)
    {
        if (children.Count == 0)
        {
            return true;
        }

        switch (mode)
        {
            case CriterionGroupMode.Any:
                foreach (WorldCriterion child in children)
                {
                    if (child.Matches(tile))
                    {
                        return true;
                    }
                }

                return false;
            case CriterionGroupMode.None:
                foreach (WorldCriterion child in children)
                {
                    if (child.Matches(tile))
                    {
                        return false;
                    }
                }

                return true;
            default:
                foreach (WorldCriterion child in children)
                {
                    if (!child.Matches(tile))
                    {
                        return false;
                    }
                }

                return true;
        }
    }

    public bool Contains(WorldCriterion criterion)
    {
        foreach (WorldCriterion child in children)
        {
            if (child == criterion || (child is CriterionGroup group && group.Contains(criterion)))
            {
                return true;
            }
        }

        return false;
    }

    public CriterionGroup? ParentOf(WorldCriterion criterion)
    {
        foreach (WorldCriterion child in children)
        {
            if (child == criterion)
            {
                return this;
            }

            if (child is CriterionGroup group && group.ParentOf(criterion) is { } parent)
            {
                return parent;
            }
        }

        return null;
    }

    public override WorldCriterion Copy()
    {
        CriterionGroup copy = (CriterionGroup)MemberwiseClone();
        copy.children = children.Select(c => c.Copy()).ToList();
        return copy;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref mode, "mode");
        Scribe_Collections.Look(ref children, "children", LookMode.Deep);
        children ??= new List<WorldCriterion>();
        children.RemoveAll(c => c == null);
    }
}
