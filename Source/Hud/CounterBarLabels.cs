namespace VanillaUIPlus;

// FPS and TPS bar labels, rebuilt only when a counter changes, about once a second.
public sealed class CounterBarLabels
{
    private int labelFps = int.MinValue;
    private int labelTps = int.MinValue;
    private int labelTarget = int.MinValue;
    private string fpsLabel = string.Empty;
    private string tpsLabel = string.Empty;

    public void Draw(int fps, int tps, int target, ref float curBaseY)
    {
        if (fps != labelFps)
        {
            labelFps = fps;
            fpsLabel = $"FPS: {fps}";
        }

        if (tps != labelTps || target != labelTarget)
        {
            labelTps = tps;
            labelTarget = target;
            tpsLabel = $"TPS: {tps}({target})";
        }

        ReadoutDrawer.DrawCounterBars(fpsLabel, tpsLabel, ref curBaseY);
    }
}
