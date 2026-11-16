using UnityEngine;

namespace VanillaUIPlus;

/// <summary>
/// One row of a Gear tab stat box: icon, muted label, colored value and tooltip.
/// </summary>
public sealed class GearStat
{
    // An icon from the readout atlas (PawnReadoutDrawer.StatIcon), or -1 to use Texture.
    public int Icon = -1;
    public Texture2D? Texture;
    public string Label = string.Empty;
    public string Value = string.Empty;
    public Color Color = Color.white;
    public string Tip = string.Empty;
}
