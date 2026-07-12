using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeCommandGroupSelection
{
	internal static IReadOnlyList<BattleRuntimeCommandGroupView> BuildPlayerGroups(
		BattleStartRequest request,
		Func<string, string> resolveUnitDisplayName) =>
		BattleRuntimeCommandHudModel.BuildPlayerGroups(request, resolveUnitDisplayName);

	internal static IReadOnlyList<BattleRuntimeHeroTroopSummaryView> BuildHeroTroopSummaries(
		IReadOnlyList<BattleRuntimeCommandGroupView> groups,
		BattleRuntimeState state,
		Func<BattleRuntimeCommandGroupView, BattleEntity> resolveHeroEntity) =>
		BattleRuntimeHeroTroopSummaryModel.Build(groups, state, resolveHeroEntity);

	internal static BattleRuntimeCommandGroupView ResolveSelected(
		IReadOnlyList<BattleRuntimeCommandGroupView> groups,
		ref string selectedGroupKey,
		ISet<string> selectedGroupKeys)
	{
		IReadOnlyList<BattleRuntimeCommandGroupView> safeGroups = groups ?? Array.Empty<BattleRuntimeCommandGroupView>();
		string currentSelectedGroupKey = selectedGroupKey;
		BattleRuntimeCommandGroupView selected = safeGroups.FirstOrDefault(group => string.Equals(group.GroupKey, currentSelectedGroupKey, StringComparison.Ordinal));
		if (selected != null)
		{
			selectedGroupKeys?.Add(selected.GroupKey);
			return selected;
		}

		selected = safeGroups.FirstOrDefault();
		selectedGroupKey = selected?.GroupKey ?? "";
		if (!string.IsNullOrWhiteSpace(selectedGroupKey))
		{
			selectedGroupKeys?.Add(selectedGroupKey);
		}

		return selected;
	}
}
