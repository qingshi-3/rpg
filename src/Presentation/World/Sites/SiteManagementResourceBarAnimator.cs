using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World.Sites;

internal sealed class SiteManagementResourceBarAnimator
{
    private const int ModalOverlayBypassZIndex = 615;
    private readonly Vector2 _retreatOffset = new Vector2(0.0f, -70.0f);
    private Control _resourceBar;
    private Tween _tween;
    private Vector2 _restPosition;
    private int _restZIndex;
    private bool _retreated;
    private bool _modalOverlayBypassActive;

    internal void Bind(Control resourceBar)
    {
        _tween?.Kill();
        _tween = null;
        _resourceBar = resourceBar;
        _restPosition = Vector2.Zero;
        _restZIndex = resourceBar?.ZIndex ?? 0;
        _retreated = false;
        _modalOverlayBypassActive = false;
    }

    internal void ApplyLayout()
    {
        if (_resourceBar == null)
        {
            return;
        }

        _resourceBar.AnchorLeft = 0.0f;
        _resourceBar.AnchorTop = 0.0f;
        _resourceBar.AnchorRight = 0.0f;
        _resourceBar.AnchorBottom = 0.0f;
        _resourceBar.OffsetLeft = 12.0f;
        _resourceBar.OffsetTop = 10.0f;
        _resourceBar.OffsetRight = 442.0f;
        _resourceBar.OffsetBottom = 52.0f;
        _restPosition = Vector2.Zero;
        if (_retreated)
        {
            _resourceBar.Position = _restPosition + _retreatOffset;
        }

        ApplyModalOverlayBypass();
    }

    internal void Update(bool visible, bool retreated, Node owner, string reason)
    {
        if (_resourceBar == null)
        {
            return;
        }

        if (!visible)
        {
            HideImmediate();
            return;
        }

        _resourceBar.Visible = true;
        _resourceBar.MouseFilter = Control.MouseFilterEnum.Ignore;
        SetRetreated(retreated, owner, reason);
    }

    internal void SetModalOverlayBypass(bool active)
    {
        _modalOverlayBypassActive = active;
        ApplyModalOverlayBypass();
    }

    private void HideImmediate()
    {
        _tween?.Kill();
        _tween = null;
        _resourceBar.Visible = false;
        _resourceBar.MouseFilter = Control.MouseFilterEnum.Ignore;
        _resourceBar.Position = _restPosition;
        _resourceBar.Modulate = Colors.White;
        _retreated = false;
    }

    private void ApplyModalOverlayBypass()
    {
        if (_resourceBar == null)
        {
            return;
        }

        bool active = _modalOverlayBypassActive;
        _resourceBar.ZIndex = active ? ModalOverlayBypassZIndex : _restZIndex;
    }

    private void SetRetreated(bool retreated, Node owner, string reason)
    {
        Vector2 targetPosition = retreated ? _restPosition + _retreatOffset : _restPosition;
        Color targetModulate = retreated
            ? new Color(1.0f, 1.0f, 1.0f, 0.0f)
            : Colors.White;
        if (_retreated == retreated && _resourceBar.Position == targetPosition)
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        if (owner == null || !owner.IsInsideTree())
        {
            _resourceBar.Position = targetPosition;
            _resourceBar.Modulate = targetModulate;
            _retreated = retreated;
            return;
        }

        // Strategic resources stay visible by default; map placement owns the
        // top-left interaction space and temporarily slides this context away.
        _tween = owner.CreateTween().BindNode(owner);
        _tween.SetParallel(true);
        _tween.SetTrans(Tween.TransitionType.Cubic);
        _tween.SetEase(retreated ? Tween.EaseType.In : Tween.EaseType.Out);
        _tween.TweenProperty(_resourceBar, "position", targetPosition, 0.22);
        _tween.TweenProperty(_resourceBar, "modulate", targetModulate, 0.22);
        _retreated = retreated;
        GameLog.Info(nameof(SiteManagementResourceBarAnimator), $"ResourceBarRetreatChanged active={retreated} reason={reason ?? ""}");
    }
}
