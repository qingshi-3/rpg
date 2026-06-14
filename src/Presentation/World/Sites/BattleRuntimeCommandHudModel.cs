using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Presentation.Battle.Flow;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeCommandHudModel
{
    internal static IReadOnlyList<BattleRuntimeCommandGroupView> BuildPlayerGroups(
        BattleStartRequest request,
        Func<string, string> resolveUnitDisplayName)
    {
        List<BattleForceRequest> forces = (request?.PlayerForces ?? new List<BattleForceRequest>())
            .Where(force => force != null && force.Count > 0)
            .ToList();
        return forces
            .GroupBy(ResolveGroupKey, StringComparer.Ordinal)
            .Select(group => BuildCommandGroup(group.Key, group.ToArray(), resolveUnitDisplayName))
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupKey))
            .ToArray();
    }

    internal static BattleRuntimeCommandGroupView BuildCommandGroup(
        string groupKey,
        IReadOnlyList<BattleForceRequest> forces,
        Func<string, string> resolveUnitDisplayName)
    {
        BattleForceRequest heroForce = forces?.FirstOrDefault(IsLikelyHeroForce) ?? forces?.FirstOrDefault();
        string heroName = resolveUnitDisplayName?.Invoke(heroForce?.UnitDefinitionId ?? "") ?? "";
        return new BattleRuntimeCommandGroupView
        {
            GroupKey = groupKey ?? "",
            DisplayName = string.IsNullOrWhiteSpace(heroName) ? groupKey ?? "参战英雄" : heroName,
            HeroName = string.IsNullOrWhiteSpace(heroName) ? "英雄：参战英雄" : $"英雄：{heroName}",
            CorpsSummary = BuildCorpsSummary(forces, heroForce, resolveUnitDisplayName),
            DefaultFormationId = BattlePreparationPlanUiModel.ResolveDefaultFormationId(forces),
            Forces = forces?.ToArray() ?? Array.Empty<BattleForceRequest>()
        };
    }

    internal static string BuildCorpsSummary(
        IReadOnlyList<BattleForceRequest> forces,
        BattleForceRequest heroForce,
        Func<string, string> resolveUnitDisplayName)
    {
        List<string> corps = (forces ?? Array.Empty<BattleForceRequest>())
            .Where(force => force != null && !ReferenceEquals(force, heroForce))
            .Select(force => $"{resolveUnitDisplayName?.Invoke(force.UnitDefinitionId) ?? ""} x{force.Count}")
            .ToList();
        return corps.Count == 0
            ? "部队：无附属部队"
            : $"部队：{string.Join("，", corps)}";
    }

    internal static bool IsLikelyHeroForce(BattleForceRequest force)
    {
        return force != null &&
               (Rpg.Application.World.FirstSliceHeroCompanyIds.IsHeroUnit(force.UnitDefinitionId) ||
                force.UnitDefinitionId?.Contains("hero", StringComparison.OrdinalIgnoreCase) == true ||
                force.SourceKind?.Contains("Hero", StringComparison.OrdinalIgnoreCase) == true);
    }

    internal static string ResolveGroupKey(BattleForceRequest force)
    {
        if (force == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(force.StrategicParticipantId))
        {
            // Strategic battles command the same stable participant id that Runtime
            // assigns to actor BattleGroupId; probe ids are legacy launch adapters.
            return force.StrategicParticipantId.Trim();
        }

        string runtimeCommanderGroupId = BattleCommanderGroupIdentity.BuildProbeCommanderGroupId(
            force,
            string.IsNullOrWhiteSpace(force.ForceId) ? force.UnitDefinitionId ?? "" : force.ForceId);
        if (!string.IsNullOrWhiteSpace(runtimeCommanderGroupId))
        {
            return runtimeCommanderGroupId;
        }

        if (!string.IsNullOrWhiteSpace(force.SourceKind) && !string.IsNullOrWhiteSpace(force.SourceId))
        {
            return $"{force.SourceKind}:{force.SourceId}";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        return string.IsNullOrWhiteSpace(force.ForceId) ? force.UnitDefinitionId ?? "" : force.ForceId;
    }

    internal static CommandKind ToCommandKind(BattleCorpsCommand command)
    {
        return command == BattleCorpsCommand.HoldLine
            ? CommandKind.Hold
            : CommandKind.Attack;
    }

    internal static string NormalizeCorpsCommandId(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.FocusFire => nameof(BattleCorpsCommand.FocusFire),
            BattleCorpsCommand.HoldLine => nameof(BattleCorpsCommand.HoldLine),
            _ => nameof(BattleCorpsCommand.Assault)
        };
    }

    internal static BattleCorpsCommand ResolveCorpsCommand(string commandId)
    {
        return Enum.TryParse(commandId?.Trim() ?? "", ignoreCase: true, out BattleCorpsCommand command)
            ? command
            : BattleCorpsCommand.Assault;
    }

    internal static void ApplyRuntimeCommandToRequest(
        BattleStartRequest request,
        CommandRequest commandRequest)
    {
        if (request != null && commandRequest != null)
        {
            request.InitialCorpsCommandId = commandRequest.CommandId ?? "";
        }
    }
}
