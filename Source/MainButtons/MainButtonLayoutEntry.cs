using RimWorld;
using Verse;

namespace VanillaUIPlus;

public sealed class MainButtonLayoutEntry : IExposable
{
    public string defName = string.Empty;
    public MainButtonPlacement placement = MainButtonPlacement.Bar;
    public MainButtonLook look = MainButtonLook.TextAndIcon;
    public string iconPath = string.Empty;
    // The menu a Dropdown tab sits in; empty means More.
    public string menu = string.Empty;
    // A menu's or tab's custom name; empty means the default label.
    public string label = string.Empty;
    // Shown only while its own mod shows it, for tabs whose mod starts them hidden (e.g. Character Editor's "Show in main tabs").
    public bool followsMod;

    // Runtime state, not saved.
    internal MainButtonDef? CachedDef;
    // Hidden in a layout saved before followsMod existed, so it may have been hidden only because its mod hid it at startup.
    internal bool LegacyHidden;
    internal readonly MainButtonDrawCache DrawCache = new MainButtonDrawCache();

    public void ExposeData()
    {
        Scribe_Values.Look(ref defName, "defName", string.Empty);
        defName ??= string.Empty;
        Scribe_Values.Look(ref placement, "placement", MainButtonPlacement.Bar);
        Scribe_Values.Look(ref look, "look", MainButtonLook.Unset);
        if (look == MainButtonLook.Unset)
        {
            look = MainButtonLook.TextAndIcon;
        }

        Scribe_Values.Look(ref iconPath, "iconPath", string.Empty);
        iconPath ??= string.Empty;
        Scribe_Values.Look(ref menu, "menu", string.Empty);
        menu ??= string.Empty;
        Scribe_Values.Look(ref label, "label", string.Empty);
        label ??= string.Empty;
        if (Scribe.mode == LoadSaveMode.LoadingVars && Scribe.loader.curXmlParent?["followsMod"] == null)
        {
            LegacyHidden = placement == MainButtonPlacement.Hidden;
        }

        Scribe_Values.Look(ref followsMod, "followsMod", false);
    }
}
