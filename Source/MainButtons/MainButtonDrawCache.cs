namespace VanillaUIPlus;

// The label measurement for one button, reused until its width, look, label or icon changes.
public sealed class MainButtonDrawCache
{
    public float Width = -1f;
    public MainButtonLook Look;
    public string Label = string.Empty;
    public string Drawn = string.Empty;
    public float TextLeft = -1f;
    public string IconPath = string.Empty;
}
