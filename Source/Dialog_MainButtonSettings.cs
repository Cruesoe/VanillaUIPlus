using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_MainButtonSettings : Dialog_ListingSettings
{
    public override Vector2 InitialSize => new Vector2(
        Mathf.Min(980f, UI.screenWidth - 40f),
        Mathf.Min(760f, UI.screenHeight - 40f));

    protected override string TitleKey => "VUIP.ConfigureMainBar";

    protected override void Reset()
    {
        MainButtonLayout.ResetToDefaults();
        UiPlusMod.Instance.WriteSettings();
    }

    protected override void DrawContents(Listing_Standard list, float width)
    {
        MainButtonLayout.DrawSettings(list);
    }
}
