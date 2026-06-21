using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle.Commands;
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
        string sourceDefinitionId = "")
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

            if (IsChanneledSkillCastPresentation(sourceDefinitionId) &&
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

        if (!SuppressActorAttachedSkillCastFx(sourceDefinitionId))
        {
            actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx(actionDurationSeconds);
        }

        actor.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Attack);
        return actionDurationSeconds;
    }

    private static bool IsChanneledSkillCastPresentation(string sourceDefinitionId)
    {
        return string.Equals(
            sourceDefinitionId ?? "",
            HeroSkillCommandIds.ThunderSpiralBreakSkillId,
            System.StringComparison.Ordinal);
    }

    private static bool SuppressActorAttachedSkillCastFx(string sourceDefinitionId)
    {
        // Thunder Spiral Break owns a separate authored area FX at the submitted
        // 3x3 center; keeping the generic actor-attached cast ring makes the
        // release read as centered on the caster.
        return string.Equals(
            sourceDefinitionId ?? "",
            HeroSkillCommandIds.ThunderSpiralBreakSkillId,
            System.StringComparison.Ordinal);
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

    public double PlayThunderTagPresentation(
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

    public double PlayThunderSpiralBreakPresentation(
        BattleEntity actor,
        GridSurfacePosition targetSurface,
        double durationSeconds)
    {
        if (actor == null ||
            !GodotObject.IsInstanceValid(actor) ||
            !TryResolveThunderSpiralAreaGlobalPosition(targetSurface, out Vector2 areaGlobal))
        {
            return 0;
        }

        PackedScene scene = GD.Load<PackedScene>(DefaultThunderSpiralFxScenePath);
        if (scene?.Instantiate() is not Node2D fx)
        {
            return 0;
        }

        BattleThunderSpiralFx spiralFx = fx as BattleThunderSpiralFx;
        spiralFx?.ConfigureAreaCoreSize(ResolveThunderSpiralAreaPixelSize(targetSurface.Position));

        AddChild(fx);
        fx.GlobalPosition = areaGlobal + ThunderSpiralAreaOffset;
        spiralFx?.Play(durationSeconds);

        return System.Math.Max(0, durationSeconds);
    }

    private bool TryResolveThunderSpiralAreaGlobalPosition(
        GridSurfacePosition targetSurface,
        out Vector2 areaGlobal)
    {
        areaGlobal = default;
        // Runtime stores Thunder Spiral Break's submitted 3x3 area as a target
        // center cell. Presentation resolves that coarse center only; it does
        // not derive a hand socket, preview cell, or secondary damage area.
        return _tryResolveCellGlobalPosition?.Invoke(targetSurface.Position, out areaGlobal) == true;
    }

    private Vector2 ResolveThunderSpiralAreaPixelSize(GridPosition targetCenter)
    {
        // Thunder Spiral Break is a fixed 3x3 Runtime area. The release point
        // only chooses the center; the authored vortex is tuned larger for
        // readability but remains anchored to this center cell.
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
