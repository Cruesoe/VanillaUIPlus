using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// One condition in an advanced world search. Defs are kept by defName, since saved searches load before defs exist.
/// </summary>
public abstract class WorldCriterion : IExposable
{
    public const float ControlRowHeight = 28f;

    public abstract string Label { get; }

    /// <summary>Why this criterion can't be used (a missing DLC), or null.</summary>
    public virtual string? LockedReason => null;

    public virtual float ControlsHeight => 0f;

    public virtual void DrawControls(Rect rect, ref bool changed)
    {
    }

    public abstract bool Matches(Tile tile);

    public virtual WorldCriterion Copy()
    {
        return (WorldCriterion)MemberwiseClone();
    }

    public virtual void ExposeData()
    {
    }

    protected static string Titled(string title, string name)
    {
        return title + ": " + name;
    }
}
