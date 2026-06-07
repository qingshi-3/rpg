using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

internal static class BattlePreparationDeploymentRouting
{
    public static string ResolvePlayerFactionId(BattleStartRequest request, string runtimePlayerFactionId)
    {
        string forceFactionId = request?.PlayerForces?
            .FirstOrDefault(force => !string.IsNullOrWhiteSpace(force?.FactionId))
            ?.FactionId;
        if (!string.IsNullOrWhiteSpace(forceFactionId))
        {
            return forceFactionId;
        }

        return !string.IsNullOrWhiteSpace(request?.AttackerFactionId)
            ? request.AttackerFactionId
            : runtimePlayerFactionId ?? "";
    }

    public static string ResolveEnemyFactionId(BattleStartRequest request, string playerFactionId)
    {
        string forceFactionId = request?.EnemyForces?
            .FirstOrDefault(force => !string.IsNullOrWhiteSpace(force?.FactionId))
            ?.FactionId;
        if (!string.IsNullOrWhiteSpace(forceFactionId))
        {
            return forceFactionId;
        }

        foreach (string factionId in new[] { request?.DefenderFactionId, request?.AttackerFactionId })
        {
            if (!string.IsNullOrWhiteSpace(factionId) &&
                !string.Equals(factionId, playerFactionId, System.StringComparison.Ordinal))
            {
                return factionId;
            }
        }

        return "";
    }

    public static WorldSiteAttackDirection ResolveDirection(
        BattleStartRequest request,
        SemanticDeploymentSide deploymentSide,
        string factionId)
    {
        WorldSiteAttackDirection attackDirection = request?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (attackDirection == WorldSiteAttackDirection.Any)
        {
            return WorldSiteAttackDirection.Any;
        }

        string factionKey = factionId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(factionKey) &&
            string.Equals(factionKey, request?.DefenderFactionId, System.StringComparison.Ordinal))
        {
            return GetOppositeDirection(attackDirection);
        }

        if (!string.IsNullOrWhiteSpace(factionKey) &&
            string.Equals(factionKey, request?.AttackerFactionId, System.StringComparison.Ordinal))
        {
            return attackDirection;
        }

        bool defenderSide = request?.BattleKind switch
        {
            BattleKind.AssaultSite => deploymentSide == SemanticDeploymentSide.Enemy,
            BattleKind.FieldIntercept => deploymentSide == SemanticDeploymentSide.Enemy,
            _ => deploymentSide == SemanticDeploymentSide.Player
        };

        return defenderSide ? GetOppositeDirection(attackDirection) : attackDirection;
    }

    public static SemanticDeploymentSide ResolveSide(
        BattleStartRequest request,
        string factionId,
        BattleFaction fallbackFaction,
        string playerDeploymentFactionId,
        string enemyDeploymentFactionId)
    {
        if (fallbackFaction == BattleFaction.Player)
        {
            return SemanticDeploymentSide.Player;
        }

        if (fallbackFaction == BattleFaction.Enemy)
        {
            return SemanticDeploymentSide.Enemy;
        }

        string factionKey = factionId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(factionKey))
        {
            return SemanticDeploymentSide.Any;
        }

        if ((request?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
            .Any(force => string.Equals(force?.FactionId, factionKey, System.StringComparison.Ordinal)))
        {
            return SemanticDeploymentSide.Player;
        }

        if ((request?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>())
            .Any(force => string.Equals(force?.FactionId, factionKey, System.StringComparison.Ordinal)))
        {
            return SemanticDeploymentSide.Enemy;
        }

        if (string.Equals(factionKey, playerDeploymentFactionId, System.StringComparison.Ordinal))
        {
            return SemanticDeploymentSide.Player;
        }

        if (string.Equals(factionKey, enemyDeploymentFactionId, System.StringComparison.Ordinal))
        {
            return SemanticDeploymentSide.Enemy;
        }

        return SemanticDeploymentSide.Any;
    }

    public static WorldSiteAttackDirection GetOppositeDirection(WorldSiteAttackDirection direction)
    {
        return direction switch
        {
            WorldSiteAttackDirection.North => WorldSiteAttackDirection.South,
            WorldSiteAttackDirection.South => WorldSiteAttackDirection.North,
            WorldSiteAttackDirection.West => WorldSiteAttackDirection.East,
            WorldSiteAttackDirection.East => WorldSiteAttackDirection.West,
            _ => WorldSiteAttackDirection.Any
        };
    }
}
