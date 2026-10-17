using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class Dialog_NameWorldSearch : Window
{
    private const int MaxNameLength = 40;
    private readonly CriterionGroup query;
    private string name;
    private bool focused;

    public Dialog_NameWorldSearch(CriterionGroup query)
    {
        this.query = query;
        name = UniqueName();
        forcePause = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new Vector2(360f, 150f);

    private static string UniqueName()
    {
        List<SavedWorldSearch> saved = UiPlusMod.Settings.savedWorldSearches;
        for (int i = 1; ; i++)
        {
            string candidate = "VUIP.WorldQueryDefaultName".Translate(i);
            if (!saved.Any(s => s.name == candidate))
            {
                return candidate;
            }
        }
    }

    private bool Valid => !name.Trim().NullOrEmpty();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f), "VUIP.WorldQueryNamePrompt".Translate());
        GUI.SetNextControlName("VUIP.WorldSearchName");
        name = Widgets.TextField(new Rect(inRect.x, inRect.y + 30f, inRect.width, 30f), name);
        if (name.Length > MaxNameLength)
        {
            name = name.Substring(0, MaxNameLength);
        }
        if (!focused)
        {
            UI.FocusControl("VUIP.WorldSearchName", this);
            focused = true;
        }

        bool replaces = UiPlusMod.Settings.savedWorldSearches.Any(s => s.name == name.Trim());
        Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(4f, 0f), "Cancel".Translate()))
        {
            Close();
        }

        string accept = (replaces ? "VUIP.WorldQueryReplace" : "Save").Translate();
        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(4f, 0f), accept, active: Valid))
        {
            OnAcceptKeyPressed();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (!Valid)
        {
            return;
        }

        WorldQueryPanel.Save(name.Trim(), query);
        Close();
    }
}
