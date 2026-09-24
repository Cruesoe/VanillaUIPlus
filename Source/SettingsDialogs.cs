using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_MainButtonSettings : Window
{
    private Vector2 scroll;
    private float viewHeight;

    public Dialog_MainButtonSettings()
    {
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(
        Mathf.Min(980f, UI.screenWidth - 40f),
        Mathf.Min(760f, UI.screenHeight - 40f));

    public override void DoWindowContents(Rect inRect)
    {
        DrawHeader(inRect, "VUIP.ConfigureMainBar", delegate
        {
            MainButtonLayout.ResetToDefaults();
            UiPlusMod.Instance.WriteSettings();
        });

        Rect outRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
        float viewWidth = outRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(viewHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, view);
        Listing_Standard list = new Listing_Standard { maxOneColumn = true };
        list.Begin(view);
        MainButtonLayout.DrawSettings(list);
        list.End();
        viewHeight = list.CurHeight + 12f;
        Widgets.EndScrollView();
    }

    private static void DrawHeader(Rect inRect, string titleKey, System.Action reset)
    {
        Rect row = new Rect(inRect.x, inRect.y, inRect.width, 36f);
        Rect resetRect = new Rect(row.xMax - 120f, row.y + 3f, 120f, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height), titleKey.Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (Widgets.ButtonText(resetRect, "Reset".Translate()))
        {
            reset();
        }
    }
}

public sealed class Dialog_PlayButtonSettings : Window
{
    private Vector2 scroll;
    private float viewHeight;

    public Dialog_PlayButtonSettings()
    {
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(
        Mathf.Min(720f, UI.screenWidth - 40f),
        Mathf.Min(560f, UI.screenHeight - 40f));

    public override void DoWindowContents(Rect inRect)
    {
        Rect row = new Rect(inRect.x, inRect.y, inRect.width, 36f);
        Rect resetRect = new Rect(row.xMax - 120f, row.y + 3f, 120f, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height), "VUIP.ConfigurePlayButtons".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (Widgets.ButtonText(resetRect, "Reset".Translate()))
        {
            UiPlusMod.Settings.showPlayButtons.Clear();
            PlayButtonFilter.NotifyChanged();
            UiPlusMod.Instance.WriteSettings();
        }

        Rect outRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
        float viewWidth = outRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(viewHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, view);
        Listing_Standard list = new Listing_Standard { maxOneColumn = true };
        list.Begin(view);
        PlayButtonFilter.DrawSettings(list, viewWidth);
        list.End();
        viewHeight = list.CurHeight + 12f;
        Widgets.EndScrollView();
    }
}
