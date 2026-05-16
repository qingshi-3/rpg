using System;
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleSimulation
{
    private readonly int _maxTicks;
    private readonly int _healthPerUnit;
    private readonly int _attackDamage;
    private readonly int _attackRange;
    private readonly int _attackCooldownTicks;

    public AutoBattleSimulation(AutoBattleSimulationConfig config = null)
    {
        config ??= new AutoBattleSimulationConfig();
        _maxTicks = Math.Max(1, config.MaxTicks);
        _healthPerUnit = Math.Max(1, config.HealthPerUnit);
        _attackDamage = Math.Max(1, config.AttackDamage);
        _attackRange = Math.Max(0, config.AttackRange);
        _attackCooldownTicks = Math.Max(0, config.AttackCooldownTicks);
    }

    public AutoBattleSimulationResult RunToEnd(BattleStartRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        AutoBattleRuntimeState state = new();
        List<AutoBattleEvent> events = new();
        events.Add(new AutoBattleEvent
        {
            Tick = 0,
            Kind = AutoBattleEventKind.BattleStarted
        });

        SpawnForces(request.PlayerForces, isPlayerSide: true, "player", state, events);
        SpawnForces(request.EnemyForces, isPlayerSide: false, "enemy", state, events);

        state.Outcome = ResolveOutcome(state);
        for (int tick = 0; state.Outcome == BattleOutcome.None && tick < _maxTicks; tick++)
        {
            state.CurrentTick = tick;
            foreach (AutoBattleCombatant combatant in state.MutableCombatants)
            {
                ProcessCombatant(combatant, state, events);
            }

            DecrementCooldowns(state);
            state.Outcome = ResolveOutcome(state);
        }

        if (state.Outcome == BattleOutcome.None)
        {
            state.Outcome = BattleOutcome.Disaster;
            state.CurrentTick = _maxTicks;
        }

        events.Add(new AutoBattleEvent
        {
            Tick = state.CurrentTick,
            Kind = AutoBattleEventKind.BattleEnded,
            Outcome = state.Outcome
        });

        BattleResult result = BuildBattleResult(request, state);
        return new AutoBattleSimulationResult(state, events.AsReadOnly(), result);
    }

    private void SpawnForces(
        IReadOnlyList<BattleForceRequest> forces,
        bool isPlayerSide,
        string fallbackSide,
        AutoBattleRuntimeState state,
        List<AutoBattleEvent> events)
    {
        if (forces == null)
        {
            return;
        }

        for (int forceIndex = 0; forceIndex < forces.Count; forceIndex++)
        {
            BattleForceRequest force = forces[forceIndex] ?? throw new InvalidOperationException(
                $"auto_battle_null_force side={fallbackSide} index={forceIndex}");
            int count = Math.Max(0, force.Count);
            string forceId = ResolveForceId(force, fallbackSide, forceIndex);
            for (int unitIndex = 0; unitIndex < count; unitIndex++)
            {
                BattleForcePlacementRequest placement = ResolvePlacement(force, forceId, unitIndex);
                string combatantId = $"{forceId}:{unitIndex}";
                AutoBattleCombatant combatant = new(
                    combatantId,
                    forceId,
                    force.SourceKind,
                    force.SourceId,
                    force.UnitDefinitionId,
                    force.FactionId,
                    isPlayerSide,
                    placement.CellX,
                    placement.CellY,
                    placement.CellHeight,
                    _healthPerUnit);

                state.MutableCombatants.Add(combatant);
                events.Add(new AutoBattleEvent
                {
                    Tick = 0,
                    Kind = AutoBattleEventKind.UnitSpawned,
                    ActorId = combatant.CombatantId,
                    ForceId = combatant.ForceId,
                    UnitDefinitionId = combatant.UnitDefinitionId,
                    CellX = combatant.CellX,
                    CellY = combatant.CellY,
                    CellHeight = combatant.CellHeight,
                    RemainingHealth = combatant.Health
                });
            }
        }
    }

    private void ProcessCombatant(
        AutoBattleCombatant combatant,
        AutoBattleRuntimeState state,
        List<AutoBattleEvent> events)
    {
        if (combatant.IsDefeated)
        {
            return;
        }

        AutoBattleCombatant target = ResolveCurrentTarget(combatant, state);
        if (target == null)
        {
            return;
        }

        if (!string.Equals(combatant.CurrentTargetId, target.CombatantId, StringComparison.Ordinal))
        {
            combatant.CurrentTargetId = target.CombatantId;
            events.Add(new AutoBattleEvent
            {
                Tick = state.CurrentTick,
                Kind = AutoBattleEventKind.TargetAcquired,
                ActorId = combatant.CombatantId,
                TargetId = target.CombatantId,
                ForceId = combatant.ForceId,
                UnitDefinitionId = combatant.UnitDefinitionId,
                CellX = combatant.CellX,
                CellY = combatant.CellY,
                CellHeight = combatant.CellHeight
            });
        }

        if (ManhattanDistance(combatant, target) > _attackRange)
        {
            MoveTowardTarget(combatant, target, state, events);
            return;
        }

        if (combatant.AttackCooldownTicksRemaining > 0)
        {
            return;
        }

        ResolveAttack(combatant, target, state, events);
    }

    private AutoBattleCombatant ResolveCurrentTarget(AutoBattleCombatant combatant, AutoBattleRuntimeState state)
    {
        AutoBattleCombatant current = state.MutableCombatants.FirstOrDefault(item =>
            string.Equals(item.CombatantId, combatant.CurrentTargetId, StringComparison.Ordinal));
        if (current != null && !current.IsDefeated && current.IsPlayerSide != combatant.IsPlayerSide)
        {
            return current;
        }

        return state.MutableCombatants
            .Where(item => item.IsPlayerSide != combatant.IsPlayerSide && !item.IsDefeated)
            .OrderBy(item => ManhattanDistance(combatant, item))
            .ThenBy(item => item.CombatantId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private void MoveTowardTarget(
        AutoBattleCombatant combatant,
        AutoBattleCombatant target,
        AutoBattleRuntimeState state,
        List<AutoBattleEvent> events)
    {
        events.Add(new AutoBattleEvent
        {
            Tick = state.CurrentTick,
            Kind = AutoBattleEventKind.MovementStarted,
            ActorId = combatant.CombatantId,
            TargetId = target.CombatantId,
            ForceId = combatant.ForceId,
            UnitDefinitionId = combatant.UnitDefinitionId,
            CellX = combatant.CellX,
            CellY = combatant.CellY,
            CellHeight = combatant.CellHeight
        });

        int deltaX = target.CellX - combatant.CellX;
        int deltaY = target.CellY - combatant.CellY;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY) && deltaX != 0)
        {
            combatant.CellX += Math.Sign(deltaX);
        }
        else if (deltaY != 0)
        {
            combatant.CellY += Math.Sign(deltaY);
        }

        events.Add(new AutoBattleEvent
        {
            Tick = state.CurrentTick,
            Kind = AutoBattleEventKind.MovementCompleted,
            ActorId = combatant.CombatantId,
            TargetId = target.CombatantId,
            ForceId = combatant.ForceId,
            UnitDefinitionId = combatant.UnitDefinitionId,
            CellX = combatant.CellX,
            CellY = combatant.CellY,
            CellHeight = combatant.CellHeight
        });
    }

    private void ResolveAttack(
        AutoBattleCombatant combatant,
        AutoBattleCombatant target,
        AutoBattleRuntimeState state,
        List<AutoBattleEvent> events)
    {
        target.Health = Math.Max(0, target.Health - _attackDamage);
        combatant.AttackCooldownTicksRemaining = _attackCooldownTicks;
        events.Add(new AutoBattleEvent
        {
            Tick = state.CurrentTick,
            Kind = AutoBattleEventKind.AttackResolved,
            ActorId = combatant.CombatantId,
            TargetId = target.CombatantId,
            ForceId = combatant.ForceId,
            UnitDefinitionId = combatant.UnitDefinitionId,
            Damage = _attackDamage,
            RemainingHealth = target.Health,
            CellX = target.CellX,
            CellY = target.CellY,
            CellHeight = target.CellHeight
        });

        if (target.IsDefeated)
        {
            events.Add(new AutoBattleEvent
            {
                Tick = state.CurrentTick,
                Kind = AutoBattleEventKind.UnitDefeated,
                ActorId = target.CombatantId,
                TargetId = combatant.CombatantId,
                ForceId = target.ForceId,
                UnitDefinitionId = target.UnitDefinitionId,
                CellX = target.CellX,
                CellY = target.CellY,
                CellHeight = target.CellHeight,
                RemainingHealth = target.Health
            });
        }
    }

    private void DecrementCooldowns(AutoBattleRuntimeState state)
    {
        foreach (AutoBattleCombatant combatant in state.MutableCombatants)
        {
            if (combatant.AttackCooldownTicksRemaining > 0)
            {
                combatant.AttackCooldownTicksRemaining--;
            }
        }
    }

    private BattleResult BuildBattleResult(BattleStartRequest request, AutoBattleRuntimeState state)
    {
        BattleResult result = new()
        {
            RequestId = request.RequestId,
            ContextId = request.ContextId,
            BattleKind = request.BattleKind,
            Outcome = state.Outcome
        };

        AddForceResults(result, request.PlayerForces, "player", state);
        AddForceResults(result, request.EnemyForces, "enemy", state);
        return result;
    }

    private void AddForceResults(
        BattleResult result,
        IReadOnlyList<BattleForceRequest> forces,
        string fallbackSide,
        AutoBattleRuntimeState state)
    {
        if (forces == null)
        {
            return;
        }

        for (int forceIndex = 0; forceIndex < forces.Count; forceIndex++)
        {
            BattleForceRequest force = forces[forceIndex] ?? new BattleForceRequest();
            string forceId = ResolveForceId(force, fallbackSide, forceIndex);
            int initial = Math.Max(0, force.Count);
            int survived = state.MutableCombatants.Count(item =>
                string.Equals(item.ForceId, forceId, StringComparison.Ordinal) &&
                !item.IsDefeated);

            result.ForceResults.Add(new BattleForceResult
            {
                ForceId = forceId,
                SourceKind = force.SourceKind,
                SourceId = force.SourceId,
                UnitDefinitionId = force.UnitDefinitionId,
                InitialCount = initial,
                SurvivedCount = survived,
                DefeatedCount = Math.Max(0, initial - survived)
            });
        }
    }

    private static BattleForcePlacementRequest ResolvePlacement(BattleForceRequest force, string forceId, int unitIndex)
    {
        if (force.PreferredPlacements == null || unitIndex >= force.PreferredPlacements.Count)
        {
            throw new InvalidOperationException(
                $"auto_battle_missing_preferred_placement force={forceId} index={unitIndex}");
        }

        return force.PreferredPlacements[unitIndex] ?? throw new InvalidOperationException(
            $"auto_battle_null_preferred_placement force={forceId} index={unitIndex}");
    }

    private static string ResolveForceId(BattleForceRequest force, string fallbackSide, int forceIndex)
    {
        if (!string.IsNullOrWhiteSpace(force?.ForceId))
        {
            return force.ForceId;
        }

        return $"{fallbackSide}_force_{forceIndex}";
    }

    private static BattleOutcome ResolveOutcome(AutoBattleRuntimeState state)
    {
        bool hasPlayer = state.MutableCombatants.Any(item => item.IsPlayerSide && !item.IsDefeated);
        bool hasEnemy = state.MutableCombatants.Any(item => !item.IsPlayerSide && !item.IsDefeated);

        if (!hasPlayer && !hasEnemy)
        {
            return BattleOutcome.Disaster;
        }

        if (!hasEnemy)
        {
            return BattleOutcome.Victory;
        }

        return !hasPlayer ? BattleOutcome.Defeat : BattleOutcome.None;
    }

    private static int ManhattanDistance(AutoBattleCombatant actor, AutoBattleCombatant target)
    {
        return Math.Abs(actor.CellX - target.CellX) + Math.Abs(actor.CellY - target.CellY);
    }
}
