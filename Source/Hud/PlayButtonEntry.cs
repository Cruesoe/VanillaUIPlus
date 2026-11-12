using UnityEngine;

namespace VanillaUIPlus;

public sealed class PlayButtonEntry
{
    public string Id = string.Empty;
    public Texture2D? Texture;
    public string Tooltip = string.Empty;
    // An on/off toggle, which can have a default state for new games.
    public bool IsToggle;
}
