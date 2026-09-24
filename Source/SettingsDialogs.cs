using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>A settings dialog with a title, a Reset button and a scrolling listing.</summary>
public abstract class Dialog_ListingSettings : Window
{
    private Vector2 scroll;
    private float viewHeight;

    protected Dialog_ListingSettings()
    {
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
    }

    protected abstract string TitleKey { get; }

    protected abstract void Reset();

    protected abstract void DrawContents(Listing_Standard list, float width);

    public override void DoWindowContents(Rect inRect)
    {
        SettingsWidgets.Header(new Rect(inRect.x, inRect.y, inRect.width, 36f), TitleKey.Translate(), "Reset".Translate(), null, Reset);

        Rect outRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
        float viewWidth = outRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(viewHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, view);
        Listing_Standard list = new Listing_Standard { maxOneColumn = true };
        list.Begin(view);
        DrawContents(list, viewWidth);
        list.End();
        viewHeight = list.CurHeight + 12f;
        Widgets.EndScrollView();
    }
}

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
