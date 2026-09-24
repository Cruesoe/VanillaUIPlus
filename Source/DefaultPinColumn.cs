using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// A pin beside a pawn table's copy/paste buttons: filled when the pawn matches the saved default, click to set or clear it.
/// </summary>
public abstract class PawnColumnWorker_DefaultPin : PawnColumnWorker
{
    private const float IconSize = 20f;

    // Loaded on first draw: column workers are created off the main thread, where textures can't load.
    private static Texture2D? pinTex;
    private static Texture2D? pinOutlineTex;

    private static Texture2D PinTex =>
        pinTex ??= ContentFinder<Texture2D>.Get("UI/Developer/Pin", reportFailure: false) ?? TexButton.Save;

    private static Texture2D PinOutlineTex =>
        pinOutlineTex ??= ContentFinder<Texture2D>.Get("UI/Developer/Pin-Outline", reportFailure: false) ?? TexButton.Save;

    private string? setTip;
    private string? clearTip;

    protected abstract bool FeatureEnabled { get; }

    protected abstract string SetTipKey { get; }

    protected abstract string ClearTipKey { get; }

    protected abstract string SetMessageKey { get; }

    protected abstract bool CanPin(Pawn pawn);

    protected abstract bool IsDefault(Pawn pawn);

    protected abstract void SetDefault(Pawn pawn);

    protected abstract void ClearDefault();

    public override bool VisibleCurrently => FeatureEnabled && base.VisibleCurrently;

    public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
    {
        if (!CanPin(pawn))
        {
            return;
        }

        Rect button = new Rect(
            rect.x + (rect.width - IconSize) / 2f,
            rect.y + (rect.height - IconSize) / 2f,
            IconSize,
            IconSize);
        bool isDefault = IsDefault(pawn);
        Color color = isDefault ? Color.white : Widgets.InactiveColor;
        string tip = isDefault ? clearTip ??= ClearTipKey.Translate() : setTip ??= SetTipKey.Translate();
        if (Widgets.ButtonImage(button, isDefault ? PinTex : PinOutlineTex, color, tooltip: tip))
        {
            if (isDefault)
            {
                ClearDefault();
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            else
            {
                SetDefault(pawn);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message(SetMessageKey.Translate(pawn.LabelShortCap), MessageTypeDefOf.SilentInput, historical: false);
            }
        }
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
