using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Animation;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Entities;

public partial class UnitAnimationComponent : BattleEntityComponent
{
    [Export]
    public NodePath AnimationPlayerPath { get; set; } = new("AnimationPlayer");

    [Export]
    public NodePath AnimatedSpritePath { get; set; } = new("VisualRoot/AnimatedSprite2D");

    [Export]
    public BattleUnitAnimationSet AnimationSet { get; set; }

    [Export]
    public NodePath VisualRootPath { get; set; } = new("VisualRoot");

    [Export]
    public bool EnableProceduralFallback { get; set; }

    [Export]
    public bool LogMissingConfiguredAnimations { get; set; } = true;

    private readonly HashSet<string> _loggedMissingKeys = new();
    private AnimationPlayer _animationPlayer;
    private AnimationPlayer _connectedAnimationPlayer;
    private AnimatedSprite2D _animatedSprite;
    private AnimatedSprite2D _connectedAnimatedSprite;
    private Node2D _visualRoot;
    private Tween _proceduralTween;
    private string _proceduralCue = "";
    private int _proceduralCueVersion;
    private bool _animationFinishedConnected;
    private bool _spriteAnimationFinishedConnected;
    private bool _facingRight = true;
    private string _defeatedAnimationName = "";
    private double _defeatedMinimumDurationSeconds;
    private int _defeatedVersion;
    private int _oneShotReturnVersion;
    private System.Action _defeatedFinished;

    protected override void OnAttached()
    {
        ResolveAnimationPlayer();
        ResolveVisualRoot();
        ResolveAnimatedSprite();
        ApplyFacing();
        TryConnectAnimationFinished();
        TryConnectSpriteAnimationFinished();
        PlayIdle();

        GameLog.Info(
            nameof(UnitAnimationComponent),
            $"Attached entity={Entity?.EntityId} hasSet={AnimationSet != null} hasPlayer={_animationPlayer != null} hasSprite={_animatedSprite != null} hasVisualRoot={_visualRoot != null}");
    }

    public override void _ExitTree()
    {
        DisconnectAnimationFinished();
        DisconnectSpriteAnimationFinished();
        KillProceduralTween();

        _animationPlayer = null;
        _animatedSprite = null;
        _visualRoot = null;
        _defeatedFinished = null;
    }

    private void DisconnectAnimationFinished()
    {
        if (_animationFinishedConnected &&
            _connectedAnimationPlayer != null &&
            GodotObject.IsInstanceValid(_connectedAnimationPlayer))
        {
            _connectedAnimationPlayer.AnimationFinished -= OnAnimationFinished;
        }

        _connectedAnimationPlayer = null;
        _animationFinishedConnected = false;
    }

    private void DisconnectSpriteAnimationFinished()
    {
        if (_spriteAnimationFinishedConnected &&
            _connectedAnimatedSprite != null &&
            GodotObject.IsInstanceValid(_connectedAnimatedSprite))
        {
            _connectedAnimatedSprite.AnimationFinished -= OnAnimatedSpriteAnimationFinished;
        }

        _connectedAnimatedSprite = null;
        _spriteAnimationFinishedConnected = false;
    }

    public bool PlayIdle()
    {
        return TryPlay(AnimationSet?.IdleAnimation, "idle", restart: false);
    }

    public bool PlayMove(bool restart = true)
    {
        return TryPlay(AnimationSet?.MoveAnimation, "move", restart);
    }

    public bool PlayAttack()
    {
        return TryPlay(AnimationSet?.AttackAnimation, "attack", restart: true);
    }

    public double ResolveAttackDurationSeconds()
    {
        string attackAnimation = ResolveAnimationName(AnimationSet?.AttackAnimation, "attack");
        return ResolveAnimationDurationSeconds(attackAnimation, "attack");
    }

    public double ResolveAttackImpactDelaySeconds()
    {
        double attackSeconds = ResolveAttackDurationSeconds();
        double normalizedTime = System.Math.Clamp(AnimationSet?.AttackImpactNormalizedTime ?? 0.55, 0, 1);
        return System.Math.Max(0, attackSeconds * normalizedTime);
    }

    public double ResolveMinimumHitDurationSeconds(double attackDurationSeconds)
    {
        double ratio = System.Math.Clamp(AnimationSet?.HitMinimumAttackDurationRatio ?? 0.4, 0, 1);
        return System.Math.Max(0, attackDurationSeconds * ratio);
    }

    public double ResolveMinimumDefeatedDurationSeconds(double attackDurationSeconds)
    {
        double ratio = System.Math.Clamp(AnimationSet?.DefeatedMinimumAttackDurationRatio ?? 0.4, 0, 1);
        return System.Math.Max(0, attackDurationSeconds * ratio);
    }

    public bool PlayHit(double minimumDurationSeconds = 0)
    {
        return TryPlay(AnimationSet?.HitAnimation, "hit", restart: true, minimumTargetSeconds: minimumDurationSeconds);
    }

    public bool PlayDefeated(
        System.Action onFinished,
        double delaySeconds = 0,
        double minimumDurationSeconds = 0)
    {
        _defeatedFinished = null;
        _defeatedAnimationName = "";
        _defeatedMinimumDurationSeconds = System.Math.Max(0, minimumDurationSeconds);

        string defeatedAnimationName = ResolveAnimationName(AnimationSet?.DefeatedAnimation, "defeated");
        _defeatedFinished = onFinished;
        _defeatedAnimationName = defeatedAnimationName;
        _defeatedVersion++;

        if (delaySeconds > 0 && IsInsideTree())
        {
            _ = PlayDefeatedAfterDelay(_defeatedVersion, delaySeconds);
            return true;
        }

        return StartDefeatedAnimation(_defeatedVersion);
    }

    public void FaceToward(Vector2 targetGlobalPosition)
    {
        if (Entity == null)
        {
            return;
        }

        FaceHorizontalDirection(targetGlobalPosition.X - Entity.GlobalPosition.X);
    }

    public void FaceHorizontalDirection(float horizontalDelta)
    {
        const float epsilon = 0.01f;
        if (horizontalDelta > epsilon)
        {
            _facingRight = true;
        }
        else if (horizontalDelta < -epsilon)
        {
            _facingRight = false;
        }
        else
        {
            return;
        }

        ApplyFacing();
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

    private void ResolveVisualRoot()
    {
        if (Entity == null)
        {
            return;
        }

        string path = VisualRootPath?.ToString() ?? "";
        _visualRoot = string.IsNullOrWhiteSpace(path)
            ? null
            : Entity.GetNodeOrNull<Node2D>(VisualRootPath);
    }

    private void ResolveAnimatedSprite()
    {
        if (Entity == null)
        {
            return;
        }

        string path = AnimatedSpritePath?.ToString() ?? "";
        _animatedSprite = string.IsNullOrWhiteSpace(path)
            ? null
            : Entity.GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
    }

    private void ApplyFacing()
    {
        if (_animatedSprite == null)
        {
            ResolveAnimatedSprite();
        }

        if (_animatedSprite == null)
        {
            return;
        }

        _animatedSprite.FlipH = !_facingRight;
    }

    private void TryConnectAnimationFinished()
    {
        if (_animationPlayer == null)
        {
            DisconnectAnimationFinished();
            return;
        }

        if (_animationFinishedConnected && _connectedAnimationPlayer == _animationPlayer)
        {
            return;
        }

        DisconnectAnimationFinished();
        _animationPlayer.AnimationFinished += OnAnimationFinished;
        _connectedAnimationPlayer = _animationPlayer;
        _animationFinishedConnected = true;
    }

    private void TryConnectSpriteAnimationFinished()
    {
        if (_animatedSprite == null)
        {
            DisconnectSpriteAnimationFinished();
            return;
        }

        if (_spriteAnimationFinishedConnected && _connectedAnimatedSprite == _animatedSprite)
        {
            return;
        }

        DisconnectSpriteAnimationFinished();
        _animatedSprite.AnimationFinished += OnAnimatedSpriteAnimationFinished;
        _connectedAnimatedSprite = _animatedSprite;
        _spriteAnimationFinishedConnected = true;
    }

    private bool TryPlay(string animationName, string cue, bool restart, double minimumTargetSeconds = 0)
    {
        string resolvedAnimationName = ResolveAnimationName(animationName, cue);
        if (!restart && IsCueAlreadyPlaying(cue, resolvedAnimationName))
        {
            return true;
        }

        bool preferAnimatedSprite = AnimationSet?.PreferAnimatedSprite != false;
        if (preferAnimatedSprite)
        {
            if (TryPlayAnimatedSprite(resolvedAnimationName, cue, restart, logMissing: false, minimumTargetSeconds) ||
                TryPlayAnimationPlayer(resolvedAnimationName, cue, restart, logMissing: false))
            {
                return true;
            }

            TryPlayAnimatedSprite(resolvedAnimationName, cue, restart, logMissing: true, minimumTargetSeconds);
            TryPlayAnimationPlayer(resolvedAnimationName, cue, restart, logMissing: true);
            return PlayProceduralCue(cue, restart, minimumTargetSeconds);
        }

        if (TryPlayAnimationPlayer(resolvedAnimationName, cue, restart, logMissing: false) ||
            TryPlayAnimatedSprite(resolvedAnimationName, cue, restart, logMissing: false, minimumTargetSeconds))
        {
            return true;
        }

        TryPlayAnimationPlayer(resolvedAnimationName, cue, restart, logMissing: true);
        TryPlayAnimatedSprite(resolvedAnimationName, cue, restart, logMissing: true, minimumTargetSeconds);
        return PlayProceduralCue(cue, restart, minimumTargetSeconds);
    }

    private bool TryPlayAnimationPlayer(string animationName, string cue, bool restart, bool logMissing)
    {
        if (_animationPlayer == null)
        {
            if (logMissing)
            {
                LogMissingOnce($"player:{cue}", $"Unit animation player missing entity={Entity?.EntityId} cue={cue} path={AnimationPlayerPath}");
            }

            return false;
        }

        if (!_animationPlayer.HasAnimation(animationName))
        {
            if (logMissing)
            {
                LogMissingOnce($"animation:{cue}:{animationName}", $"Unit animation missing entity={Entity?.EntityId} cue={cue} animation={animationName}");
            }

            return false;
        }

        bool hadProceduralTween = _proceduralTween != null;
        KillProceduralTween();
        if (hadProceduralTween)
        {
            ResetVisualRoot();
        }

        StopAnimatedSprite();
        if (!restart && _animationPlayer.IsPlaying() && _animationPlayer.CurrentAnimation == animationName)
        {
            return true;
        }

        _animationPlayer.Play(animationName);
        HandleCueStarted(cue, animationName, ResolveAnimationPlayerAnimationSeconds(animationName));
        GameLog.Info(nameof(UnitAnimationComponent), $"Animation played entity={Entity?.EntityId} cue={cue} animation={animationName}");
        return true;
    }

    private bool TryPlayAnimatedSprite(string animationName, string cue, bool restart, bool logMissing)
    {
        return TryPlayAnimatedSprite(animationName, cue, restart, logMissing, minimumTargetSeconds: 0);
    }

    private bool TryPlayAnimatedSprite(
        string animationName,
        string cue,
        bool restart,
        bool logMissing,
        double minimumTargetSeconds)
    {
        if (_animatedSprite == null)
        {
            ResolveAnimatedSprite();
            TryConnectSpriteAnimationFinished();
        }

        if (_animatedSprite == null)
        {
            if (logMissing && AnimationSet?.PreferAnimatedSprite != false)
            {
                LogMissingOnce($"sprite:{cue}", $"Unit animated sprite missing entity={Entity?.EntityId} cue={cue} path={AnimatedSpritePath}");
            }

            return false;
        }

        SpriteFrames spriteFrames = _animatedSprite.SpriteFrames;
        if (spriteFrames == null)
        {
            if (logMissing)
            {
                LogMissingOnce($"sprite-frames:{cue}", $"Unit animated sprite has no SpriteFrames entity={Entity?.EntityId} cue={cue} path={_animatedSprite.GetPath()}");
            }

            return false;
        }

        var spriteAnimationName = new StringName(animationName);
        if (!spriteFrames.HasAnimation(spriteAnimationName))
        {
            if (logMissing)
            {
                LogMissingOnce($"sprite-animation:{cue}:{animationName}", $"Unit animated sprite missing animation entity={Entity?.EntityId} cue={cue} animation={animationName} path={_animatedSprite.GetPath()}");
            }

            return false;
        }

        bool hadProceduralTween = _proceduralTween != null;
        KillProceduralTween();
        if (hadProceduralTween)
        {
            ResetVisualRoot();
        }

        StopAnimationPlayer();
        ApplyAnimatedSpriteLoopMode(spriteFrames, spriteAnimationName, cue);
        var playbackTiming = ApplyAnimatedSpritePlaybackSpeed(animationName, cue, minimumTargetSeconds);
        if (!restart &&
            _animatedSprite.IsPlaying() &&
            string.Equals(_animatedSprite.Animation.ToString(), animationName, System.StringComparison.Ordinal))
        {
            return true;
        }

        _animatedSprite.Play(spriteAnimationName);
        HandleCueStarted(cue, animationName, ResolveAnimatedSpriteAnimationSeconds(animationName));
        GameLog.Info(
            nameof(UnitAnimationComponent),
            $"Animated sprite played entity={Entity?.EntityId} cue={cue} animation={animationName} frames={playbackTiming.FrameCount} authoredSeconds={playbackTiming.AuthoredSeconds:0.00} targetSeconds={playbackTiming.TargetSeconds:0.00} speedScale={playbackTiming.SpeedScale:0.00} path={_animatedSprite.GetPath()}");
        return true;
    }

    private static void ApplyAnimatedSpriteLoopMode(SpriteFrames spriteFrames, StringName animationName, string cue)
    {
        if (spriteFrames == null)
        {
            return;
        }

        if (cue is "attack" or "hit" or "defeated")
        {
            spriteFrames.SetAnimationLoop(animationName, false);
        }
        else if (cue is "idle" or "move")
        {
            spriteFrames.SetAnimationLoop(animationName, true);
        }
    }

    private void HandleCueStarted(string cue, string animationName, double authoredSeconds)
    {
        if (!ShouldReturnToIdleAfterCue(cue))
        {
            InvalidateOneShotReturn();
            return;
        }

        int version = InvalidateOneShotReturn();
        double delaySeconds = authoredSeconds > 0 ? authoredSeconds + 0.05 : ResolveTargetSpriteSeconds(cue) + 0.05;
        QueueOneShotReturnToIdle(version, animationName, delaySeconds);
    }

    private int InvalidateOneShotReturn()
    {
        unchecked
        {
            _oneShotReturnVersion++;
        }

        return _oneShotReturnVersion;
    }

    private async void QueueOneShotReturnToIdle(int version, string animationName, double delaySeconds)
    {
        if (AnimationSet?.ReturnToIdleAfterOneShot == false || !IsInsideTree() || delaySeconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _oneShotReturnVersion &&
            Entity != null &&
            !BattleRuleQueries.IsDefeated(Entity) &&
            IsCurrentAnimation(animationName))
        {
            PlayIdle();
        }
    }

    private (int FrameCount, double AuthoredSeconds, double TargetSeconds, float SpeedScale) ApplyAnimatedSpritePlaybackSpeed(
        string animationName,
        string cue,
        double minimumTargetSeconds)
    {
        if (_animatedSprite == null ||
            !TryResolveAnimatedSpriteAnimationTiming(animationName, out int frameCount, out double authoredSeconds))
        {
            return (0, 0, 0, 1f);
        }

        double targetSeconds = ResolveTargetSpriteSeconds(cue, minimumTargetSeconds);
        bool shouldBalance = AnimationSet?.BalanceSpriteSpeedByFrameCount ?? true;
        float speedScale = ResolveBalancedSpriteSpeedScale(authoredSeconds, targetSeconds);

        _animatedSprite.SpeedScale = speedScale;
        return (frameCount, authoredSeconds, shouldBalance ? targetSeconds : 0, speedScale);
    }

    private float ResolveBalancedSpriteSpeedScale(double authoredSeconds, double targetSeconds)
    {
        bool shouldBalance = AnimationSet?.BalanceSpriteSpeedByFrameCount ?? true;
        if (!shouldBalance || targetSeconds <= 0 || authoredSeconds <= 0)
        {
            return 1f;
        }

        float minScale = AnimationSet?.MinBalancedSpeedScale ?? 1f;
        float maxScale = AnimationSet?.MaxBalancedSpeedScale ?? 4.5f;
        if (maxScale < minScale)
        {
            maxScale = minScale;
        }

        return (float)System.Math.Clamp(authoredSeconds / targetSeconds, minScale, maxScale);
    }

    private double ResolveAnimationDurationSeconds(string animationName, string cue)
    {
        if (!string.IsNullOrWhiteSpace(animationName) &&
            TryResolveAnimatedSpriteAnimationTiming(animationName, out _, out double authoredSeconds))
        {
            float speedScale = ResolveBalancedSpriteSpeedScale(authoredSeconds, ResolveTargetSpriteSeconds(cue));
            return speedScale > 0 ? authoredSeconds / speedScale : authoredSeconds;
        }

        if (_animationPlayer != null &&
            GodotObject.IsInstanceValid(_animationPlayer) &&
            !string.IsNullOrWhiteSpace(animationName) &&
            _animationPlayer.HasAnimation(animationName))
        {
            Animation animation = _animationPlayer.GetAnimation(animationName);
            if (animation != null && animation.Length > 0)
            {
                return animation.Length;
            }
        }

        return ResolveTargetSpriteSeconds(cue);
    }

    private double ResolveTargetSpriteSeconds(string cue)
    {
        return ResolveTargetSpriteSeconds(cue, minimumTargetSeconds: 0);
    }

    private double ResolveTargetSpriteSeconds(string cue, double minimumTargetSeconds)
    {
        double targetSeconds = cue switch
        {
            "idle" => AnimationSet?.TargetIdleCycleSeconds ?? 1.2,
            "move" => AnimationSet?.TargetMoveCycleSeconds ?? 0.5,
            "attack" => AnimationSet?.TargetAttackSeconds ?? 1.2,
            "hit" => AnimationSet?.TargetHitSeconds ?? 0.48,
            "defeated" => AnimationSet?.TargetDefeatedSeconds ?? 0.8,
            _ => 0
        };

        return cue is "hit" or "defeated"
            ? System.Math.Max(targetSeconds, minimumTargetSeconds)
            : targetSeconds;
    }

    private bool PlayProceduralCue(string cue, bool restart, double minimumTargetSeconds = 0)
    {
        if (!EnableProceduralFallback || _visualRoot == null)
        {
            return false;
        }

        if (!restart && _proceduralTween != null && _proceduralCue == cue)
        {
            return true;
        }

        KillProceduralTween();
        StopAnimationPlayer();
        StopAnimatedSprite();
        _proceduralCue = cue;
        _proceduralCueVersion++;
        InvalidateOneShotReturn();
        int version = _proceduralCueVersion;

        ResetVisualRoot();
        _proceduralTween = CreateTween();
        _proceduralTween.SetTrans(Tween.TransitionType.Sine);
        _proceduralTween.SetEase(Tween.EaseType.InOut);

        switch (cue)
        {
            case "idle":
                _proceduralTween.SetLoops();
                _proceduralTween.TweenProperty(_visualRoot, "position", new Vector2(0f, -2f), 0.55);
                _proceduralTween.TweenProperty(_visualRoot, "position", Vector2.Zero, 0.55);
                break;

            case "move":
                _proceduralTween.SetLoops();
                _proceduralTween.TweenProperty(_visualRoot, "scale", new Vector2(1.06f, 0.94f), 0.16);
                _proceduralTween.TweenProperty(_visualRoot, "scale", Vector2.One, 0.16);
                break;

            case "attack":
                _proceduralTween.TweenProperty(_visualRoot, "position", new Vector2(8f, -2f), 0.08);
                _proceduralTween.TweenProperty(_visualRoot, "position", Vector2.Zero, 0.14);
                QueueProceduralReturnToIdle(version, 0.24);
                break;

            case "hit":
                double hitSeconds = System.Math.Max(0.24, minimumTargetSeconds);
                double hitFlashSeconds = System.Math.Min(0.1, hitSeconds * 0.25);
                _proceduralTween.TweenProperty(_visualRoot, "modulate", new Color(1f, 0.55f, 0.55f, 1f), hitFlashSeconds);
                _proceduralTween.TweenProperty(_visualRoot, "modulate", Colors.White, System.Math.Max(0.1, hitSeconds - hitFlashSeconds));
                QueueProceduralReturnToIdle(version, hitSeconds + 0.02);
                break;

            case "defeated":
                _proceduralTween.TweenProperty(_visualRoot, "rotation", 0.18f, 0.16);
                _proceduralTween.TweenProperty(_visualRoot, "scale", new Vector2(0.86f, 0.86f), 0.16);
                _proceduralTween.Parallel().TweenProperty(_visualRoot, "modulate", new Color(1f, 1f, 1f, 0.38f), 0.28);
                break;

            default:
                _proceduralTween.Kill();
                _proceduralTween = null;
                _proceduralCue = "";
                return false;
        }

        GameLog.Info(nameof(UnitAnimationComponent), $"Procedural animation played entity={Entity?.EntityId} cue={cue}");
        return true;
    }

    private async void QueueProceduralReturnToIdle(int version, double delaySeconds)
    {
        if (AnimationSet?.ReturnToIdleAfterOneShot == false || !IsInsideTree())
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _proceduralCueVersion && Entity != null && !BattleRuleQueries.IsDefeated(Entity))
        {
            PlayIdle();
        }
    }

    private void ResetVisualRoot()
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.Position = Vector2.Zero;
        _visualRoot.Scale = Vector2.One;
        _visualRoot.Rotation = 0f;
        _visualRoot.Modulate = Colors.White;
    }

    private void KillProceduralTween()
    {
        if (_proceduralTween != null || !string.IsNullOrEmpty(_proceduralCue))
        {
            _proceduralCueVersion++;
        }

        if (_proceduralTween != null && GodotObject.IsInstanceValid(_proceduralTween))
        {
            _proceduralTween.Kill();
        }

        _proceduralTween = null;
        _proceduralCue = "";
    }

    private void StopAnimationPlayer()
    {
        if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
        {
            _animationPlayer.Stop();
        }
    }

    private void StopAnimatedSprite()
    {
        if (_animatedSprite != null && GodotObject.IsInstanceValid(_animatedSprite))
        {
            _animatedSprite.Stop();
        }
    }

    private void OnAnimationFinished(StringName animationName)
    {
        HandleFinishedAnimation(animationName.ToString());
    }

    private void OnAnimatedSpriteAnimationFinished()
    {
        string finished = _animatedSprite?.Animation.ToString() ?? "";
        HandleFinishedAnimation(finished);
    }

    private void HandleFinishedAnimation(string finished)
    {
        if (!string.IsNullOrWhiteSpace(_defeatedAnimationName) &&
            finished == _defeatedAnimationName)
        {
            CompleteDefeatedAnimation();
            return;
        }

        if (AnimationSet?.ReturnToIdleAfterOneShot != false &&
            (finished == ResolveAnimationName(AnimationSet?.AttackAnimation, "attack") ||
             finished == ResolveAnimationName(AnimationSet?.HitAnimation, "hit")))
        {
            PlayIdle();
        }
    }

    private static string ResolveAnimationName(string configuredName, string cue)
    {
        return string.IsNullOrWhiteSpace(configuredName)
            ? cue
            : configuredName;
    }

    private static bool ShouldReturnToIdleAfterCue(string cue)
    {
        return cue is "attack" or "hit";
    }

    private bool IsCurrentAnimation(string animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        if (_animatedSprite != null &&
            GodotObject.IsInstanceValid(_animatedSprite) &&
            string.Equals(_animatedSprite.Animation.ToString(), animationName, System.StringComparison.Ordinal))
        {
            return true;
        }

        return _animationPlayer != null &&
               GodotObject.IsInstanceValid(_animationPlayer) &&
               string.Equals(_animationPlayer.CurrentAnimation, animationName, System.StringComparison.Ordinal);
    }

    private bool IsCueAlreadyPlaying(string cue, string animationName)
    {
        if (!string.IsNullOrWhiteSpace(_proceduralCue) &&
            _proceduralTween != null &&
            string.Equals(_proceduralCue, cue, System.StringComparison.Ordinal))
        {
            return true;
        }

        return IsCurrentAnimation(animationName);
    }

    private double ResolveAnimationPlayerAnimationSeconds(string animationName)
    {
        if (_animationPlayer == null ||
            !GodotObject.IsInstanceValid(_animationPlayer) ||
            string.IsNullOrWhiteSpace(animationName))
        {
            return 0;
        }

        double seconds = _animationPlayer.CurrentAnimationLength;
        return seconds > 0 ? seconds : 0;
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
        _defeatedMinimumDurationSeconds = 0;
        callback?.Invoke();
    }

    private async Task PlayDefeatedAfterDelay(int version, double delaySeconds)
    {
        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (version != _defeatedVersion)
        {
            return;
        }

        if (!StartDefeatedAnimation(version))
        {
            CompleteDefeatedAnimation();
        }
    }

    private bool StartDefeatedAnimation(int version)
    {
        if (version != _defeatedVersion || string.IsNullOrWhiteSpace(_defeatedAnimationName))
        {
            return false;
        }

        if (!TryPlay(_defeatedAnimationName, "defeated", restart: true, minimumTargetSeconds: _defeatedMinimumDurationSeconds))
        {
            _defeatedFinished = null;
            _defeatedAnimationName = "";
            _defeatedMinimumDurationSeconds = 0;
            return false;
        }

        _ = CompleteDefeatedAfterFallbackDelay(version, ResolveDefeatedFallbackSeconds());
        return true;
    }

    private double ResolveDefeatedFallbackSeconds()
    {
        double configured = AnimationSet?.DefeatedFallbackSeconds ?? 0.65;
        double spriteLength = ResolveAnimatedSpriteAnimationSeconds(_defeatedAnimationName);
        if (spriteLength > 0)
        {
            return System.Math.Max(spriteLength, _defeatedMinimumDurationSeconds) + 0.05;
        }

        if (_animationPlayer == null || string.IsNullOrWhiteSpace(_defeatedAnimationName))
        {
            return System.Math.Max(configured, _defeatedMinimumDurationSeconds);
        }

        double currentLength = _animationPlayer.CurrentAnimationLength;
        return currentLength > 0
            ? System.Math.Max(currentLength, _defeatedMinimumDurationSeconds) + 0.05
            : System.Math.Max(configured, _defeatedMinimumDurationSeconds);
    }

    private double ResolveAnimatedSpriteAnimationSeconds(string animationName)
    {
        if (!TryResolveAnimatedSpriteAnimationTiming(animationName, out _, out double authoredSeconds))
        {
            return 0;
        }

        float speedScale = _animatedSprite?.SpeedScale ?? 1f;
        return speedScale > 0 ? authoredSeconds / speedScale : authoredSeconds;
    }

    private bool TryResolveAnimatedSpriteAnimationTiming(
        string animationName,
        out int frameCount,
        out double authoredSeconds)
    {
        frameCount = 0;
        authoredSeconds = 0;
        if (_animatedSprite == null || string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        SpriteFrames spriteFrames = _animatedSprite.SpriteFrames;
        var spriteAnimationName = new StringName(animationName);
        if (spriteFrames == null || !spriteFrames.HasAnimation(spriteAnimationName))
        {
            return false;
        }

        frameCount = spriteFrames.GetFrameCount(spriteAnimationName);
        double speed = spriteFrames.GetAnimationSpeed(spriteAnimationName);
        if (frameCount <= 0 || speed <= 0)
        {
            return false;
        }

        authoredSeconds = frameCount / speed;
        return true;
    }

    private void LogMissingOnce(string key, string message)
    {
        if (!LogMissingConfiguredAnimations || !_loggedMissingKeys.Add(key))
        {
            return;
        }

        GameLog.Warn(nameof(UnitAnimationComponent), message);
    }
}
