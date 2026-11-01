using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_PlayButtonSettings : Dialog_ListingSettings
{
    public override Vector2 InitialSize => new Vector2(
        Mathf.Min(720f, UI.screenWidth - 40f),
        Mathf.Min(560f, UI.screenHeight - 40f));

    protected override string TitleKey => "VUIP.ConfigurePlayButtons";

    protected override void Reset()
    {
        UiPlusMod.Settings.showPlayButtons.Clear();
        PlayButtonFilter.NotifyChanged();
        UiPlusMod.Instance.WriteSettings();
    }

    protected override void DrawContents(Listing_Standard list, float width)
    {
        PlayButtonFilter.DrawSettings(list, width);
    }
}
