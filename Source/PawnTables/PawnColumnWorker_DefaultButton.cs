using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// A button beside a pawn table's copy/paste buttons: bright when the pawn matches the saved default, click for a menu to set, load or clear it.
/// </summary>
[StaticConstructorOnStartup]
public abstract class PawnColumnWorker_DefaultButton : PawnColumnWorker
{
    private const float IconSize = 20f;

    // Loaded on first draw: column workers are created off the main thread, where textures can't load. The policy window's "set as default" icon.
    private static Texture2D? icon;

    private static Texture2D Icon =>
        icon ??= ContentFinder<Texture2D>.Get("UI/Buttons/MakeDefault", reportFailure: false) ?? TexButton.Save;

    private string? tip;
    private string? matchesTip;

    protected abstract bool FeatureEnabled { get; }

    protected abstract bool IsSet { get; }

    protected abstract string TipKey { get; }

    protected abstract string SetMessageKey { get; }

    protected abstract string LoadedMessageKey { get; }

    protected abstract bool CanUse(Pawn pawn);

    protected abstract bool IsDefault(Pawn pawn);

    protected abstract void SetDefault(Pawn pawn);

    protected abstract void LoadDefault(Pawn pawn);

    protected abstract void ClearDefault();

    public override bool VisibleCurrently => FeatureEnabled && base.VisibleCurrently;

    public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
    {
        if (!CanUse(pawn))
        {
            return;
        }

        Rect button = new Rect(
            rect.x + (rect.width - IconSize) / 2f,
            rect.y + (rect.height - IconSize) / 2f,
            IconSize,
            IconSize);
        bool isDefault = IsDefault(pawn);
        tip ??= TipKey.Translate();
        matchesTip ??= tip + "\n\n" + "VUIP.DefaultMatchesTip".Translate();
        if (Widgets.ButtonImage(button, Icon, isDefault ? Color.white : Widgets.InactiveColor, tooltip: isDefault ? matchesTip : tip))
        {
            Find.WindowStack.Add(new FloatMenu(MenuOptions(pawn, isDefault)));
        }
    }

    // Built only when the button is clicked.
    private List<FloatMenuOption> MenuOptions(Pawn pawn, bool isDefault)
    {
        bool isSet = IsSet;
        string alreadyDefault = " (" + "VUIP.DefaultAlreadyMatches".Translate() + ")";
        string noDefault = " (" + "VUIP.DefaultNotSet".Translate() + ")";

        FloatMenuOption set = isDefault
            ? new FloatMenuOption("VUIP.DefaultMenuSet".Translate() + alreadyDefault, null)
            : new FloatMenuOption("VUIP.DefaultMenuSet".Translate(), () =>
            {
                SetDefault(pawn);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message(SetMessageKey.Translate(pawn.LabelShortCap), MessageTypeDefOf.SilentInput, historical: false);
            });

        FloatMenuOption load = !isSet
            ? new FloatMenuOption("VUIP.DefaultMenuLoad".Translate() + noDefault, null)
            : isDefault
                ? new FloatMenuOption("VUIP.DefaultMenuLoad".Translate() + alreadyDefault, null)
                : new FloatMenuOption("VUIP.DefaultMenuLoad".Translate(), () =>
                {
                    LoadDefault(pawn);
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Messages.Message(LoadedMessageKey.Translate(pawn.LabelShortCap), MessageTypeDefOf.SilentInput, historical: false);
                });

        FloatMenuOption clear = !isSet
            ? new FloatMenuOption("VUIP.DefaultMenuClear".Translate() + noDefault, null)
            : new FloatMenuOption("VUIP.DefaultMenuClear".Translate(), () =>
            {
                ClearDefault();
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            });

        return new List<FloatMenuOption> { set, load, clear };
    }

    public override int GetMinWidth(PawnTable table)
    {
        return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(IconSize) + 4);
    }

    public override int GetMaxWidth(PawnTable table)
    {
        return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
    }
}
