using Godot;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeCommandHudPresentation
{
    internal static void SetProgressBar(ProgressBar progressBar, int value, int maxValue)
    {
        if (progressBar == null)
        {
            return;
        }

        int safeMax = System.Math.Max(1, maxValue);
        progressBar.MinValue = 0;
        progressBar.MaxValue = safeMax;
        progressBar.Value = System.Math.Clamp(value, 0, safeMax);
    }
}
