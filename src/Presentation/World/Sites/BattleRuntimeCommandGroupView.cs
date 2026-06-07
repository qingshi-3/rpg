using System.Collections.Generic;
using Rpg.Application.Battle;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeCommandGroupView
{
    public string GroupKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string HeroName { get; init; } = "";
    public string CorpsSummary { get; init; } = "";
    public string DefaultFormationId { get; init; } = "";
    public IReadOnlyList<BattleForceRequest> Forces { get; init; } = System.Array.Empty<BattleForceRequest>();
}
