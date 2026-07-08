using System;
using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.World.Sites;

internal static class SiteManagementCenteredModalAnimator
{
    private const float ModalLeftEdgeNudgePixels = 0.0f;
    private const float ModalPointScale = 0.06f;
    private const float ModalOpenOvershootScale = 1.015f;
    private const float ModalOpenOvershootViewportRatio = 0.60f;
    private const float ModalCenterArrivalScale = 0.54f;
    private const float ModalUiLaunchOpacity = 0.24f;
    private const float ModalUiCenterOpacity = 0.72f;
    // Keep the panel path near 0.385s while giving launch most of the budget,
    // so the point-like left-edge growth reads before the faster rebound/settle.
    // The close path reuses these tuned opening timings in reverse so
    // later path tuning keeps the q-bounce enter and exit visually paired.
    private const double TabRetractDelaySeconds = 0.10;
    private const double ModalOpenBackdropFadeSeconds = 0.135;
    private const double ModalUiLaunchSeconds = 0.27;
    private const double ModalOpenReturnSeconds = 0.07;
    private const double ModalOpenSettleSeconds = 0.045;
    private static readonly Color Transparent = new(1.0f, 1.0f, 1.0f, 0.0f);
    private static readonly Color Opaque = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Dictionary<ulong, ModalAnimationState> ActiveModalAnimations = new();

    private sealed class ModalAnimationState
    {
        internal Tween ActiveTween;
        internal int Generation;
        internal Vector2 RestPosition;
        internal bool HasRestPosition;
    }

    internal static void OpenCenteredModalAfterDelay(Node owner, Control panel, Control backdrop, Action bindContent)
    {
        if (!ControlsAreValid(panel, backdrop))
        {
            bindContent?.Invoke();
            return;
        }

        Vector2 restPosition = ResolveAnimationRestPosition(panel);
        int generation = BeginAnimation(panel, restPosition);
        if (!CanAnimateWithOwner(owner))
        {
            OpenCenteredModal(owner, panel, backdrop, bindContent, generation, restPosition);
            return;
        }

        Tween tween = owner.CreateTween().BindNode(owner);
        SetActiveTween(panel, generation, tween);
        tween.TweenInterval(TabRetractDelaySeconds);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!IsCurrent(panel, generation) || !ControlsAreValid(panel, backdrop))
            {
                return;
            }

            OpenCenteredModal(owner, panel, backdrop, bindContent, generation, restPosition);
        }));
    }

    internal static void OpenCenteredModal(Node owner, Control panel, Control backdrop, Action bindContent = null)
    {
        if (!ControlsAreValid(panel, backdrop))
        {
            bindContent?.Invoke();
            return;
        }

        Vector2 restPosition = ResolveAnimationRestPosition(panel);
        int generation = BeginAnimation(panel, restPosition);
        OpenCenteredModal(owner, panel, backdrop, bindContent, generation, restPosition);
    }

    internal static void CloseCenteredModal(Node owner, Control panel, Control backdrop, Action afterClosed)
    {
        if (!ControlsAreValid(panel, backdrop))
        {
            afterClosed?.Invoke();
            return;
        }

        Vector2 restPosition = ResolveAnimationRestPosition(panel);
        if (!panel.Visible || !CanAnimateWithOwner(owner))
        {
            CancelCenteredModal(panel, backdrop, () => afterClosed?.Invoke());
            return;
        }

        int generation = BeginAnimation(panel, restPosition);
        Vector2 startPosition = ResolveLeftEdgeStartPosition(restPosition, panel);
        Vector2 centerOvershootPosition = ResolveCenterOvershootPosition(startPosition, restPosition, panel);
        Tween tween = owner.CreateTween().BindNode(panel);
        SetActiveTween(panel, generation, tween);
        tween.TweenProperty(panel, "scale", new Vector2(ModalOpenOvershootScale, ModalOpenOvershootScale), ModalOpenSettleSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Chain().TweenProperty(panel, "position", centerOvershootPosition, ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(panel, "scale", new Vector2(ModalCenterArrivalScale, ModalCenterArrivalScale), ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(panel, "modulate", new Color(1.0f, 1.0f, 1.0f, ModalUiCenterOpacity), ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Chain().TweenProperty(panel, "position", startPosition, ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(panel, "scale", new Vector2(ModalPointScale, ModalPointScale), ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "modulate", Transparent, ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(backdrop, "modulate", Transparent, ModalOpenBackdropFadeSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (!IsCurrent(panel, generation) || !ControlsAreValid(panel, backdrop))
            {
                return;
            }

            HideCenteredModal(panel, backdrop, restPosition);
            FinishAnimation(panel, generation);
            afterClosed?.Invoke();
        }));
    }

    internal static void CancelCenteredModal(Control panel, Control backdrop, Action afterCanceled = null)
    {
        if (!ControlsAreValid(panel, backdrop))
        {
            afterCanceled?.Invoke();
            return;
        }

        Vector2 restPosition = ResolveAnimationRestPosition(panel);
        int generation = BeginAnimation(panel, restPosition);
        HideCenteredModal(panel, backdrop, restPosition);
        FinishAnimation(panel, generation);
        afterCanceled?.Invoke();
    }

    private static void OpenCenteredModal(Node owner, Control panel, Control backdrop, Action bindContent, int generation, Vector2 restPosition)
    {
        if (!IsCurrent(panel, generation) || !ControlsAreValid(panel, backdrop))
        {
            return;
        }

        Vector2 startPosition = ResolveLeftEdgeStartPosition(restPosition, panel);
        Vector2 centerOvershootPosition = ResolveCenterOvershootPosition(startPosition, restPosition, panel);
        PrepareCenteredModalOpen(panel, backdrop, startPosition);
        bindContent?.Invoke();
        if (!IsCurrent(panel, generation) || !ControlsAreValid(panel, backdrop))
        {
            return;
        }

        if (!CanAnimateWithOwner(owner))
        {
            ResetCenteredModalOpen(panel, backdrop, restPosition);
            FinishAnimation(panel, generation);
            return;
        }

        Tween tween = owner.CreateTween().BindNode(panel);
        SetActiveTween(panel, generation, tween);
        tween.TweenProperty(backdrop, "modulate", Opaque, ModalOpenBackdropFadeSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "position", centerOvershootPosition, ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "scale", new Vector2(ModalCenterArrivalScale, ModalCenterArrivalScale), ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(panel, "modulate", new Color(1.0f, 1.0f, 1.0f, ModalUiCenterOpacity), ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(panel, "position", restPosition, ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "scale", new Vector2(ModalOpenOvershootScale, ModalOpenOvershootScale), ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "modulate", Opaque, ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(panel, "position", restPosition, ModalOpenSettleSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(panel, "scale", Vector2.One, ModalOpenSettleSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (!IsCurrent(panel, generation) || !ControlsAreValid(panel, backdrop))
            {
                return;
            }

            ResetCenteredModalOpen(panel, backdrop, restPosition);
            FinishAnimation(panel, generation);
        }));
    }

    private static void PrepareCenteredModalOpen(Control panel, Control backdrop, Vector2 startPosition)
    {
        backdrop.Visible = true;
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.Modulate = Transparent;
        panel.Visible = true;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        panel.PivotOffset = ResolvePivotOffset(panel);
        panel.Position = startPosition;
        panel.Scale = new Vector2(ModalPointScale, ModalPointScale);
        panel.Modulate = new Color(1.0f, 1.0f, 1.0f, ModalUiLaunchOpacity);
    }

    private static Vector2 ResolveLeftEdgeStartPosition(Vector2 restPosition, Control panel)
    {
        return new Vector2(ModalLeftEdgeNudgePixels - ResolvePivotOffset(panel).X, restPosition.Y);
    }

    private static Vector2 ResolveCenterOvershootPosition(Vector2 startPosition, Vector2 restPosition, Control panel)
    {
        Vector2 pivotOffset = ResolvePivotOffset(panel);
        if (panel.GetParent() is Control parent)
        {
            Vector2 parentSize = ResolveControlSize(parent);
            if (parentSize.X > 1.0f)
            {
                return new Vector2((parentSize.X * ModalOpenOvershootViewportRatio) - pivotOffset.X, restPosition.Y);
            }
        }

        return restPosition + ((restPosition - startPosition) * 0.20f);
    }

    private static void ResetCenteredModalOpen(Control panel, Control backdrop, Vector2 restPosition)
    {
        if (!ControlsAreValid(panel, backdrop))
        {
            return;
        }

        backdrop.Visible = true;
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.Modulate = Opaque;
        panel.Visible = true;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        panel.Position = restPosition;
        panel.Scale = Vector2.One;
        panel.Modulate = Opaque;
    }

    private static void HideCenteredModal(Control panel, Control backdrop, Vector2 restPosition)
    {
        if (panel != null && GodotObject.IsInstanceValid(panel))
        {
            panel.Position = restPosition;
            panel.Scale = Vector2.One;
            panel.Modulate = Opaque;
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.Visible = false;
        }

        if (backdrop != null && GodotObject.IsInstanceValid(backdrop))
        {
            backdrop.Modulate = Opaque;
            backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
            backdrop.Visible = false;
        }
    }

    private static Vector2 ResolveAnimationRestPosition(Control panel)
    {
        if (panel != null &&
            GodotObject.IsInstanceValid(panel) &&
            ActiveModalAnimations.TryGetValue(panel.GetInstanceId(), out ModalAnimationState state) &&
            state.HasRestPosition &&
            state.ActiveTween != null &&
            GodotObject.IsInstanceValid(state.ActiveTween) &&
            state.ActiveTween.IsValid())
        {
            return state.RestPosition;
        }

        return panel?.Position ?? Vector2.Zero;
    }

    private static int BeginAnimation(Control panel, Vector2 restPosition)
    {
        ulong key = panel.GetInstanceId();
        if (!ActiveModalAnimations.TryGetValue(key, out ModalAnimationState state))
        {
            state = new ModalAnimationState();
            ActiveModalAnimations[key] = state;
        }

        // Repeated tab clicks can leave delayed Tween callbacks queued. The
        // generation makes every older callback a no-op before it can touch UI.
        if (!state.HasRestPosition ||
            state.ActiveTween == null ||
            !GodotObject.IsInstanceValid(state.ActiveTween) ||
            !state.ActiveTween.IsValid())
        {
            state.RestPosition = restPosition;
            state.HasRestPosition = true;
        }

        state.Generation++;
        CancelActiveTween(state);
        return state.Generation;
    }

    private static void SetActiveTween(Control panel, int generation, Tween tween)
    {
        if (panel == null ||
            !GodotObject.IsInstanceValid(panel) ||
            tween == null ||
            !ActiveModalAnimations.TryGetValue(panel.GetInstanceId(), out ModalAnimationState state) ||
            state.Generation != generation)
        {
            tween?.Kill();
            return;
        }

        state.ActiveTween = tween;
    }

    private static void FinishAnimation(Control panel, int generation)
    {
        if (!IsCurrent(panel, generation))
        {
            return;
        }

        ModalAnimationState state = ActiveModalAnimations[panel.GetInstanceId()];
        state.ActiveTween = null;
    }

    private static void CancelActiveTween(ModalAnimationState state)
    {
        if (state.ActiveTween != null &&
            GodotObject.IsInstanceValid(state.ActiveTween) &&
            state.ActiveTween.IsValid())
        {
            state.ActiveTween.Kill();
        }

        state.ActiveTween = null;
    }

    private static bool IsCurrent(Control panel, int generation)
    {
        return panel != null &&
               GodotObject.IsInstanceValid(panel) &&
               ActiveModalAnimations.TryGetValue(panel.GetInstanceId(), out ModalAnimationState state) &&
               state.Generation == generation;
    }

    private static bool ControlsAreValid(Control panel, Control backdrop)
    {
        return panel != null &&
               GodotObject.IsInstanceValid(panel) &&
               backdrop != null &&
               GodotObject.IsInstanceValid(backdrop);
    }

    private static bool CanAnimateWithOwner(Node owner)
    {
        return owner != null && GodotObject.IsInstanceValid(owner) && owner.IsInsideTree();
    }

    private static Vector2 ResolvePivotOffset(Control panel)
    {
        return ResolveControlSize(panel) * 0.5f;
    }

    private static Vector2 ResolveControlSize(Control panel)
    {
        Vector2 size = panel.Size;
        if (size.X <= 1.0f || size.Y <= 1.0f)
        {
            size = panel.CustomMinimumSize;
        }

        if (size.X <= 1.0f || size.Y <= 1.0f)
        {
            size = panel.GetCombinedMinimumSize();
        }

        return size;
    }

}
