using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Animation;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class UnitAnimationComponent : BattleEntityComponent
{
    [Export]
    public NodePath AnimationPlayerPath { get; set; } = new("AnimationPlayer");

    [Export]
    public BattleUnitAnimationSet AnimationSet { get; set; }

    [Export]
    public bool LogMissingConfiguredAnimations { get; set; } = true;

    private readonly HashSet<string> _loggedMissingKeys = new();
    private AnimationPlayer _animationPlayer;
    private string _defeatedAnimationName = "";
    private int _defeatedVersion;
    private System.Action _defeatedFinished;

    protected override void OnAttached()
    {
        ResolveAnimationPlayer();
        TryConnectAnimationFinished();
        PlayIdle();

        GameLog.Info(
            nameof(UnitAnimationComponent),
            $"Attached entity={Entity?.EntityId} hasSet={AnimationSet != null} hasPlayer={_animationPlayer != null}");
    }

    public override void _ExitTree()
    {
        if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
        {
            _animationPlayer.AnimationFinished -= OnAnimationFinished;
        }

        _animationPlayer = null;
        _defeatedFinished = null;
    }

    public bool PlayIdle()
    {
        return TryPlay(AnimationSet?.IdleAnimation, "idle", restart: false);
    }

    public bool PlayMove()
    {
        return TryPlay(AnimationSet?.MoveAnimation, "move", restart: true);
    }

    public bool PlayAttack()
    {
        return TryPlay(AnimationSet?.AttackAnimation, "attack", restart: true);
    }

    public bool PlayHit()
    {
        return TryPlay(AnimationSet?.HitAnimation, "hit", restart: true);
    }

    public bool PlayDefeated(System.Action onFinished)
    {
        _defeatedFinished = null;
        _defeatedAnimationName = "";

        if (!TryPlay(AnimationSet?.DefeatedAnimation, "defeated", restart: true))
        {
            return false;
        }

        _defeatedFinished = onFinished;
        _defeatedAnimationName = AnimationSet?.DefeatedAnimation ?? "";
        _defeatedVersion++;

        if (AnimationSet?.HideAfterDefeatedAnimation == false)
        {
            _defeatedFinished = null;
            return true;
        }

        if (AnimationSet?.HideAfterDefeatedAnimation != false)
        {
            _ = CompleteDefeatedAfterFallbackDelay(_defeatedVersion, ResolveDefeatedFallbackSeconds());
        }

        return true;
    }

    private void ResolveAnimationPlayer()
    {
        if (Entity == null)
        {
            return;
        }

        string path = AnimationPlayerPath?.ToString() ?? "";
        _animationPlayer = string.IsNullOrWhiteSpace(path)
            ? null
            : Entity.GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
    }

    private void TryConnectAnimationFinished()
    {
        if (_animationPlayer == null)
        {
            return;
        }

        _animationPlayer.AnimationFinished -= OnAnimationFinished;
        _animationPlayer.AnimationFinished += OnAnimationFinished;
    }

    private bool TryPlay(string animationName, string cue, bool restart)
    {
        if (string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        if (_animationPlayer == null)
        {
            LogMissingOnce($"player:{cue}", $"Unit animation player missing entity={Entity?.EntityId} cue={cue} path={AnimationPlayerPath}");
            return false;
        }

        if (!_animationPlayer.HasAnimation(animationName))
        {
            LogMissingOnce($"animation:{cue}:{animationName}", $"Unit animation missing entity={Entity?.EntityId} cue={cue} animation={animationName}");
            return false;
        }

        if (!restart && _animationPlayer.IsPlaying() && _animationPlayer.CurrentAnimation == animationName)
        {
            return true;
        }

        _animationPlayer.Play(animationName);
        GameLog.Info(nameof(UnitAnimationComponent), $"Animation played entity={Entity?.EntityId} cue={cue} animation={animationName}");
        return true;
    }

    private void OnAnimationFinished(StringName animationName)
    {
        string finished = animationName.ToString();
        if (!string.IsNullOrWhiteSpace(_defeatedAnimationName) &&
            finished == _defeatedAnimationName)
        {
            CompleteDefeatedAnimation();
            return;
        }

        if (AnimationSet?.ReturnToIdleAfterOneShot == true &&
            (finished == AnimationSet.AttackAnimation || finished == AnimationSet.HitAnimation))
        {
            PlayIdle();
        }
    }

    private async Task CompleteDefeatedAfterFallbackDelay(int version, double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _defeatedVersion)
        {
            CompleteDefeatedAnimation();
        }
    }

    private void CompleteDefeatedAnimation()
    {
        System.Action callback = _defeatedFinished;
        _defeatedFinished = null;
        _defeatedAnimationName = "";
        callback?.Invoke();
    }

    private double ResolveDefeatedFallbackSeconds()
    {
        if (_animationPlayer == null || string.IsNullOrWhiteSpace(_defeatedAnimationName))
        {
            return AnimationSet?.DefeatedFallbackSeconds ?? 0.65;
        }

        double configured = AnimationSet?.DefeatedFallbackSeconds ?? 0.65;
        double currentLength = _animationPlayer.CurrentAnimationLength;
        return currentLength > 0 ? currentLength + 0.05 : configured;
    }

    private void LogMissingOnce(string key, string message)
    {
        if (!LogMissingConfiguredAnimations || AnimationSet == null || !_loggedMissingKeys.Add(key))
        {
            return;
        }

        GameLog.Warn(nameof(UnitAnimationComponent), message);
    }
}
