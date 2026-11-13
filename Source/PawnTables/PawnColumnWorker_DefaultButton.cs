using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// A button beside a pawn table's copy/paste buttons: bright when the pawn matches the saved default, click for a menu to set, load or clear it.
/// Slave rows work on the separate slave default.
/// </summary>
[StaticConstructorOnStartup]
public abstract class PawnColumnWorker_DefaultButton : PawnColumnWorker
{
    private const float IconSize = 20f;

    // Loaded on first draw: column workers are created off the main thread, where textures can't load. The policy window's "set as default" icon.
    private static Texture2D? icon;

    private static Texture2D Icon =>
        icon ??= ContentFinder<Texture2D>.Get("UI/Buttons/MakeDefault", reportFailure: false) ?? TexButton.Save;

    // Indexed by (slave ? 2 : 0) + (matches ? 1 : 0); built on first use.
    private readonly string?[] tips = new string?[4];

    protected abstract bool FeatureEnabled { get; }

    protected abstract string TipKey { get; }

    protected abstract string SetMessageKey { get; }

    protected abstract string LoadedMessageKey { get; }

    protected abstract bool IsSet(bool slave);

    protected abstract bool CanUse(Pawn pawn);

    protected abstract bool IsDefault(Pawn pawn, bool slave);

    protected abstract void SetDefault(Pawn pawn, bool slave);

    protected abstract void LoadDefault(Pawn pawn, bool slave);

    protected abstract void ClearDefault(bool slave);

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
        bool slave = pawn.IsSlave;
        bool isDefault = IsDefault(pawn, slave);
        if (Widgets.ButtonImage(button, Icon, isDefault ? Color.white : Widgets.InactiveColor, tooltip: Tip(slave, isDefault)))
        {
            Find.WindowStack.Add(new FloatMenu(MenuOptions(pawn, table, slave, isDefault)));
        }
    }

    private string Tip(bool slave, bool isDefault)
    {
        int index = (slave ? 2 : 0) + (isDefault ? 1 : 0);
        if (tips[index] == null)
        {
            string tip = TipKey.Translate();
            if (slave)
            {
                tip += "\n\n" + "VUIP.DefaultSlaveTip".Translate();
            }

            if (isDefault)
            {
                tip += "\n\n" + "VUIP.DefaultMatchesTip".Translate();
            }

            tips[index] = tip;
        }

        return tips[index]!;
    }

    // Built only when the button is clicked.
    private List<FloatMenuOption> MenuOptions(Pawn pawn, PawnTable table, bool slave, bool isDefault)
    {
        bool isSet = IsSet(slave);
        string alreadyMatches = " (" + "VUIP.DefaultAlreadyMatches".Translate() + ")";
        string noDefault = " (" + "VUIP.DefaultNotSet".Translate() + ")";
        string forWhom = (slave ? "VUIP.DefaultForSlaves" : "VUIP.DefaultForColonists").Translate();

        string setLabel = (slave ? "VUIP.DefaultMenuSetSlave" : "VUIP.DefaultMenuSet").Translate();
        FloatMenuOption set = isDefault
            ? new FloatMenuOption(setLabel + alreadyMatches, null)
            : new FloatMenuOption(setLabel, () =>
            {
                SetDefault(pawn, slave);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message(SetMessageKey.Translate(pawn.LabelShortCap, forWhom), MessageTypeDefOf.SilentInput, historical: false);
            });

        string loadLabel = (slave ? "VUIP.DefaultMenuLoadSlave" : "VUIP.DefaultMenuLoad").Translate();
        FloatMenuOption load = !isSet
            ? new FloatMenuOption(loadLabel + noDefault, null)
            : isDefault
                ? new FloatMenuOption(loadLabel + alreadyMatches, null)
                : new FloatMenuOption(loadLabel, () =>
                {
                    LoadDefault(pawn, slave);
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Messages.Message(LoadedMessageKey.Translate(pawn.LabelShortCap), MessageTypeDefOf.SilentInput, historical: false);
                });

        // Every pawn of the same kind in this table that doesn't already match; a copy, since loading changes what they match.
        List<Pawn> others = new List<Pawn>();
        if (isSet)
        {
            foreach (Pawn other in table.PawnsListForReading)
            {
                if (other.IsSlave == slave && CanUse(other) && !IsDefault(other, slave))
                {
                    others.Add(other);
                }
            }
        }

        string loadAllLabel = (slave ? "VUIP.DefaultMenuLoadAllSlave" : "VUIP.DefaultMenuLoadAll").Translate();
        FloatMenuOption loadAll = !isSet
            ? new FloatMenuOption(loadAllLabel + noDefault, null)
            : others.Count == 0
                ? new FloatMenuOption(loadAllLabel + " (" + "VUIP.DefaultAllMatch".Translate() + ")", null)
                : new FloatMenuOption(loadAllLabel + " (" + others.Count + ")", () =>
                {
                    foreach (Pawn other in others)
                    {
                        LoadDefault(other, slave);
                    }

                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Messages.Message("VUIP.DefaultLoadedAll".Translate(others.Count), MessageTypeDefOf.SilentInput, historical: false);
                });

        string clearLabel = (slave ? "VUIP.DefaultMenuClearSlave" : "VUIP.DefaultMenuClear").Translate();
        FloatMenuOption clear = !isSet
            ? new FloatMenuOption(clearLabel + noDefault, null)
            : new FloatMenuOption(clearLabel, () =>
            {
                ClearDefault(slave);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            });

        return new List<FloatMenuOption> { set, load, loadAll, clear };
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
