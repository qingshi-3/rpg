using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Feedback;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitRoot
{
    private const string DefaultThunderLinkFxScenePath = "res://scenes/battle/entities/fx/BattleThunderLinkFx.tscn";
    private const string DefaultThunderMarkFxScenePath = "res://scenes/battle/entities/fx/BattleThunderMarkFx.tscn";
    private const string DefaultThunderSpiralFxScenePath = "res://scenes/battle/entities/fx/BattleThunderSpiralFx.tscn";
    private static readonly Vector2 ThunderLinkSourceOffset = new(0f, -18f);
    private static readonly Vector2 ThunderMarkAttachedOffset = new(0f, -24f);
    private static readonly Vector2 ThunderMarkGroundOffset = new(0f, -10f);
    private static readonly Vector2 ThunderSpiralAreaOffset = Vector2.Zero;
    private readonly Dictionary<string, Node2D> _thunderMarksByBattleGroup = new(System.StringComparer.Ordinal);

    public double PlaySkillCastPresentation(
        BattleEntity actor,
        BattleEntity target,
        double durationSeconds = 0,
        bool preserveMovement = false,
        string presentationProfileId = "",
        bool suppressActorCastFx = false,
        bool holdCastAnimationDuringAction = false)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor))
        {
            return 0;
        }

        UnitAnimationComponent actorAnimation = actor.GetComponent<UnitAnimationComponent>();
        double actionDurationSeconds = durationSeconds > 0
            ? durationSeconds
            : actorAnimation?.ResolveSkillCastDurationSeconds() ?? 0;
        if (!preserveMovement)
        {
            StopEntityMovement(actor, snapToLogicalGrid: true);
            if (target != null && GodotObject.IsInstanceValid(target))
            {
                actorAnimation?.FaceToward(target.GlobalPosition);
            }

            if (IsChanneledSkillCastPresentation(presentationProfileId, holdCastAnimationDuringAction) &&
                actorAnimation != null &&
                actorAnimation.PlaySkillCastHoldAtFrame(actorAnimation.ResolveChanneledSkillCastHoldFrame()))
            {
                QueueHeldSkillCastResume(actorAnimation, actionDurationSeconds);
            }
            else
            {
                actorAnimation?.PlaySkillCast();
            }
        }

        if (!SuppressActorAttachedSkillCastFx(suppressActorCastFx))
        {
            actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx(actionDurationSeconds);
        }

        actor.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        return actionDurationSeconds;
    }

    private static bool IsChanneledSkillCastPresentation(
        string presentationProfileId,
        bool holdCastAnimationDuringAction)
    {
        return holdCastAnimationDuringAction ||
               string.Equals(
                   presentationProfileId ?? "",
                   "skill_channeled_area",
                   System.StringComparison.Ordinal);
    }

    private static bool SuppressActorAttachedSkillCastFx(bool suppressActorCastFx)
    {
        // Area profiles can own a separate authored world FX; the actor ring is
        // suppressed only when Runtime events carry that presentation trait.
        return suppressActorCastFx;
    }

    private void QueueHeldSkillCastResume(UnitAnimationComponent animation, double durationSeconds)
    {
        if (animation == null || !GodotObject.IsInstanceValid(animation))
        {
            return;
        }

        if (durationSeconds <= 0 || !IsInsideTree())
        {
            animation.ResumeHeldAnimationFromNextFrame();
            return;
        }

        _ = ResumeHeldSkillCastAfterDelay(animation, durationSeconds);
    }

    private async Task ResumeHeldSkillCastAfterDelay(UnitAnimationComponent animation, double durationSeconds)
    {
        await ToSignal(GetTree().CreateTimer(durationSeconds, processAlways: false), SceneTreeTimer.SignalName.Timeout);
        if (animation != null && GodotObject.IsInstanceValid(animation))
        {
            animation.ResumeHeldAnimationFromNextFrame();
        }
    }

    public double PlayMarkProjectilePresentation(
        BattleEntity actor,
        BattleEntity target,
        GridSurfacePosition targetSurface,
        string battleGroupId,
        bool attachedToTarget)
    {
        if (actor == null ||
            !GodotObject.IsInstanceValid(actor) ||
            !TryResolveThunderTargetGlobalPosition(target, targetSurface, attachedToTarget, out Vector2 targetGlobal))
        {
            return 0;
        }

        Vector2 sourceGlobal = actor.GlobalPosition + ThunderLinkSourceOffset;
        PlayThunderLink(sourceGlobal, targetGlobal);
        ShowThunderMark(target, targetGlobal, battleGroupId, attachedToTarget);
        return 0.32;
    }

    private void PlayThunderLink(Vector2 sourceGlobal, Vector2 targetGlobal)
    {
        PackedScene scene = GD.Load<PackedScene>(DefaultThunderLinkFxScenePath);
        if (scene?.Instantiate() is not Node2D fx)
        {
            return;
        }

        AddChild(fx);
        fx.GlobalPosition = sourceGlobal;
        if (fx is BattleThunderLinkFx linkFx)
        {
            linkFx.Play(targetGlobal - sourceGlobal);
        }
    }

    private void ShowThunderMark(
        BattleEntity target,
        Vector2 targetGlobal,
        string battleGroupId,
        bool attachedToTarget)
    {
        string key = string.IsNullOrWhiteSpace(battleGroupId) ? "__default" : battleGroupId.Trim();
        ClearThunderMarkPresentation(key);

        PackedScene scene = GD.Load<PackedScene>(DefaultThunderMarkFxScenePath);
        if (scene?.Instantiate() is not Node2D mark)
        {
            return;
        }

        Node2D parent = attachedToTarget && target != null && GodotObject.IsInstanceValid(target)
            ? ResolveEntityVisualParent(target)
            : this;
        parent.AddChild(mark);
        if (attachedToTarget && parent != this)
        {
            mark.Position = ThunderMarkAttachedOffset;
        }
        else
        {
            mark.GlobalPosition = targetGlobal;
        }

        if (mark is BattleThunderMarkFx markFx)
        {
            markFx.Play();
        }

        _thunderMarksByBattleGroup[key] = mark;
    }

    private bool TryResolveThunderTargetGlobalPosition(
        BattleEntity target,
        GridSurfacePosition targetSurface,
        bool attachedToTarget,
        out Vector2 targetGlobal)
    {
        targetGlobal = default;
        if (attachedToTarget && target != null && GodotObject.IsInstanceValid(target))
        {
            targetGlobal = target.GlobalPosition + ThunderMarkAttachedOffset;
            return true;
        }

        if (_tryResolveFootprintGlobalPosition?.Invoke(targetSurface.Position, Vector2I.One, out targetGlobal) == true)
        {
            targetGlobal += ThunderMarkGroundOffset;
            return true;
        }

        return false;
    }

    private static Node2D ResolveEntityVisualParent(BattleEntity target)
    {
        return target?.GetNodeOrNull<Node2D>("VisualRoot") ?? target;
    }

    public double PlayChanneledAreaPresentation(
        BattleEntity actor,
        GridSurfacePosition targetSurface,
        double durationSeconds)
    {
        if (actor == null ||
            !GodotObject.IsInstanceValid(actor) ||
            !TryResolveChanneledAreaGlobalPosition(targetSurface, out Vector2 areaGlobal))
        {
            return 0;
        }

        PackedScene scene = GD.Load<PackedScene>(DefaultThunderSpiralFxScenePath);
        if (scene?.Instantiate() is not Node2D fx)
        {
            return 0;
        }

        BattleThunderSpiralFx spiralFx = fx as BattleThunderSpiralFx;
        spiralFx?.ConfigureAreaCoreSize(ResolveChanneledAreaPixelSize(targetSurface.Position));

        AddChild(fx);
        fx.GlobalPosition = areaGlobal + ThunderSpiralAreaOffset;
        spiralFx?.Play(durationSeconds);

        return System.Math.Max(0, durationSeconds);
    }

    private bool TryResolveChanneledAreaGlobalPosition(
        GridSurfacePosition targetSurface,
        out Vector2 areaGlobal)
    {
        areaGlobal = default;
        // Runtime stores submitted area skills as a target center cell.
        // Presentation resolves that center only; it does not derive gameplay
        // legality from visuals.
        return _tryResolveCellGlobalPosition?.Invoke(targetSurface.Position, out areaGlobal) == true;
    }

    private Vector2 ResolveChanneledAreaPixelSize(GridPosition targetCenter)
    {
        // The current authored vortex is tuned larger for readability but
        // remains anchored to Runtime's submitted center cell.
        return BattleThunderSpiralFx.ResolveDefaultAreaPixelSize();
    }

    private void ClearThunderMarkPresentation(string key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            !_thunderMarksByBattleGroup.Remove(key, out Node2D mark) ||
            mark == null ||
            !GodotObject.IsInstanceValid(mark))
        {
            return;
        }

        mark.QueueFree();
    }

    private void ClearThunderMarkPresentations()
    {
        foreach (string key in _thunderMarksByBattleGroup.Keys.ToArray())
        {
            ClearThunderMarkPresentation(key);
        }
    }

    public double PlayRuntimeDamageFeedback(
        BattleEntity actor,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        bool playSkillImpactFx)
    {
        BattleDamageEvent[] resolvedDamageEvents = damageEvents?.ToArray() ?? System.Array.Empty<BattleDamageEvent>();
        _ = HitFeedbackPresenter.PlayAsync(
            actor,
            resolvedDamageEvents,
            playSkillImpactFx,
            impactDelaySecondsOverride: 0);
        return 0;
    }
}
