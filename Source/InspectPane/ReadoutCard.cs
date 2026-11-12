using System;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// One card in a readout that isn't about a pawn (growing zones, buildings, items, storage): icon, caption, value, tooltip and an optional click.
/// </summary>
public sealed class ReadoutCard
{
    public string Caption = string.Empty;
    public string Value = string.Empty;
    public Color ValueColor = Color.white;
    // Stat icon from the readout atlas; used when no texture, thing or def is set.
    public int Icon = -1;
    public Texture2D? IconTexture;
    public Thing? IconInstance;
    public ThingDef? IconThing;
    public Func<string> Tip = () => string.Empty;
    public Action? Click;
    // Card rows this card spans (1 or 2); a taller card wraps its value.
    public int Rows = 1;

    // Tooltip text built on first hover and kept for the card's lifetime (its snapshot).
    private string? tipText;

    public string TipText()
    {
        return tipText ??= Tip();
    }
}
