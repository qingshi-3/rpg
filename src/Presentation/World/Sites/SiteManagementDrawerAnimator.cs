using System;
using Godot;

namespace Rpg.Presentation.World.Sites;

internal static class SiteManagementDrawerAnimator
{
    internal static void ApplyTabDrawerState(Button tab, string expandedText, bool expanded, bool animated)
    {
        if (tab == null)
        {
            return;
        }

        const float collapsedLeft = -52.0f;
        const float collapsedRight = 44.0f;
        const float expandedLeft = 0.0f;
        const float expandedRight = 96.0f;
        float targetLeft = expanded ? expandedLeft : collapsedLeft;
        float targetRight = expanded ? expandedRight : collapsedRight;
        tab.Text = expanded ? expandedText : "";
        tab.TooltipText = "";
        tab.IconAlignment = HorizontalAlignment.Right;
        tab.Alignment = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        if (!animated || !tab.IsInsideTree())
        {
            tab.OffsetLeft = targetLeft;
            tab.OffsetRight = targetRight;
            return;
        }

        Tween tween = tab.CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetParallel(true);
        tween.TweenProperty(tab, "offset_left", targetLeft, 0.12);
        tween.TweenProperty(tab, "offset_right", targetRight, 0.12);
    }

    internal static void OpenPanelAfterTabRetracts(Node owner, Control panel, Button button, string expandedText, Action showPanel)
    {
        ApplyTabDrawerState(button, expandedText, expanded: false, animated: true);
        if (owner == null || !owner.IsInsideTree())
        {
            showPanel();
            BouncePanelIn(owner, panel);
            return;
        }

        Tween tween = owner.CreateTween().BindNode(owner);
        tween.TweenInterval(0.12);
        tween.TweenCallback(Callable.From(() =>
        {
            showPanel();
            BouncePanelIn(owner, panel);
        }));
    }

    internal static void BouncePanelIn(Node owner, Control panel)
    {
        if (panel == null || !panel.Visible)
        {
            return;
        }

        const float panelHiddenLeft = -640.0f;
        const float panelHiddenRight = 0.0f;
        const float panelOvershootLeft = 18.0f;
        const float panelOvershootRight = 658.0f;
        const float panelFinalLeft = 0.0f;
        const float panelFinalRight = 640.0f;
        panel.OffsetLeft = panelHiddenLeft;
        panel.OffsetRight = panelHiddenRight;
        if (owner == null || !owner.IsInsideTree())
        {
            panel.OffsetLeft = panelFinalLeft;
            panel.OffsetRight = panelFinalRight;
            return;
        }

        Tween tween = owner.CreateTween().BindNode(panel);
        tween.TweenProperty(panel, "offset_left", panelOvershootLeft, 0.16)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "offset_right", panelOvershootRight, 0.16)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(panel, "offset_left", panelFinalLeft, 0.10)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "offset_right", panelFinalRight, 0.10)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    internal static void ClosePanelThenShowRail(Node owner, Control panel, Control rail, Action hidePanel, Action showRail)
    {
        if (panel == null || !panel.Visible || owner == null || !owner.IsInsideTree())
        {
            hidePanel?.Invoke();
            showRail?.Invoke();
            AnimateRailTabsIn(owner, rail);
            return;
        }

        // Closing uses the reverse spring: a small right bump makes the panel feel
        // loaded before it retracts left and hands interaction back to the rail.
        const float panelCloseOvershootLeft = 18.0f;
        const float panelCloseOvershootRight = 658.0f;
        const float panelClosedLeft = -640.0f;
        const float panelClosedRight = 0.0f;
        Tween tween = owner.CreateTween().BindNode(panel);
        tween.TweenProperty(panel, "offset_left", panelCloseOvershootLeft, 0.08)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "offset_right", panelCloseOvershootRight, 0.08)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(panel, "offset_left", panelClosedLeft, 0.16)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(panel, "offset_right", panelClosedRight, 0.16)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            hidePanel?.Invoke();
            showRail?.Invoke();
            AnimateRailTabsIn(owner, rail);
        }));
    }

    internal static void AnimateRailTabsIn(Node owner, Control rail)
    {
        if (rail == null)
        {
            return;
        }

        const float tabHiddenLeft = -96.0f;
        const float tabHiddenRight = 0.0f;
        const float tabPopLeft = -44.0f;
        const float tabPopRight = 52.0f;
        const float collapsedLeft = -52.0f;
        const float collapsedRight = 44.0f;
        int tabIndex = 0;
        foreach (Node child in rail.GetChildren())
        {
            if (child is not Button tab)
            {
                continue;
            }

            tab.Text = "";
            tab.TooltipText = "";
            tab.IconAlignment = HorizontalAlignment.Right;
            tab.Alignment = HorizontalAlignment.Center;
            tab.OffsetLeft = tabHiddenLeft;
            tab.OffsetRight = tabHiddenRight;
            if (owner == null || !owner.IsInsideTree())
            {
                tab.OffsetLeft = collapsedLeft;
                tab.OffsetRight = collapsedRight;
                continue;
            }

            Tween tween = owner.CreateTween().BindNode(tab);
            if (tabIndex > 0)
            {
                tween.TweenInterval(tabIndex * 0.025);
            }

            tween.TweenProperty(tab, "offset_left", tabPopLeft, 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(tab, "offset_right", tabPopRight, 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.Chain().TweenProperty(tab, "offset_left", collapsedLeft, 0.08)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(tab, "offset_right", collapsedRight, 0.08)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tabIndex++;
        }
    }
}
