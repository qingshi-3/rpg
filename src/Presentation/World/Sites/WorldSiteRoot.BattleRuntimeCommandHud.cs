using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Runtime.Battle;
namespace Rpg.Presentation.World.Sites;
public partial class WorldSiteRoot
{
	private const string BattleRuntimePauseAction = "battle_runtime_pause";

	internal enum BattleRuntimeSkillUsageState
	{
		Unavailable,
		Ready,
		Pending,
		Used
	}
	private enum SkillTargetingStage { None, PrimarySelection, SecondarySelection }
	private bool TryHandleBattleRuntimePauseInput(InputEvent inputEvent)
	{
		if (!_battleRuntimeEnabled ||
			_isBattlePreparationActive ||
			!inputEvent.IsActionPressed(BattleRuntimePauseAction))
		{
			return false;
		}
		ToggleBattleRuntimeCommandPause();
		GetViewport()?.SetInputAsHandled();
		return true;
	}
	private void ToggleBattleRuntimeCommandPause() =>
		SetBattleRuntimeCommandPauseActive(!_battleRuntimeCommandPauseActive, "pause_action_toggle");

	private void SubmitBattleRuntimeCommand(BattleCorpsCommand command)
	{
		CommandRequest commandRequest = BuildBattleRuntimeCommandRequest(command);
		if (_battleRuntimeEnabled && !_isBattlePreparationActive)
		{
			SetSiteNoticeText("战斗已经开始，当前姿态由战前计划决定；运行时请使用重整或英雄技能。");
			RefreshBattleRuntimeHeroFrame();
			GameLog.Info(
				nameof(WorldSiteRoot),
				$"BattleRuntimeCommandRejected command={commandRequest.CommandId} reason=runtime_locked request={commandRequest.BattleId}");
			return;
		}
		_selectedBattleCorpsCommand = command;
		BattleRuntimeCommandHudModel.ApplyRuntimeCommandToRequest(_battlePreparationRequest, commandRequest);
		if (_isBattlePreparationActive)
		{
			RefreshBattlePreparationPlanUi($"战斗姿态已设为：{BattleCorpsCommandLabels.ToDisplayText(command)}。", "battle_preparation_command_selected");
		}
		RefreshBattleRuntimeHeroFrame();
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeCommandSelected command={commandRequest.CommandId} kind={commandRequest.Kind} request={commandRequest.BattleId}");
	}
	private CommandRequest BuildBattleRuntimeCommandRequest(BattleCorpsCommand command)
	{
		BattleStartRequest request = _battlePreparationRequest;
		if (request == null && TryResolveActiveBattleRequest(out BattleStartRequest activeRequest)) { request = activeRequest; }
		return new CommandRequest
		{
			CommandId = BattleRuntimeCommandHudModel.NormalizeCorpsCommandId(command),
			BattleId = request?.RequestId ?? "",
			BattleGroupId = "player_corps_initial_posture",
			Channel = CommandChannel.Corps,
			Kind = BattleRuntimeCommandHudModel.ToCommandKind(command)
		};
	}
	private void RefreshBattleRuntimeCommandControls(bool runtimeLocked) => RefreshBattleRuntimeHeroFrame();
	private void RefreshBattleRuntimeCommandPausePresentation()
	{
		if (!_battleRuntimeCommandPauseActive)
		{
			if (_siteBottomCommandHost != null) { _siteBottomCommandHost.Visible = false; }
			if (_battleRuntimeSummaryBar != null) { _battleRuntimeSummaryBar.Visible = false; }
			if (_battleRuntimeCommandBar != null) { _battleRuntimeCommandBar.Visible = false; }
			if (_battleRuntimePauseDetailPanel != null) { _battleRuntimePauseDetailPanel.Visible = false; }
			RefreshBattleRuntimeHeroFrame();
			ApplyBattleMapOperationHudSuppressionVisibility("battle_runtime_command_resume");
			UpdateMainWorldViewportLayout("battle_runtime_command_resume");
			return;
		}
		if (_siteHudRoot != null)
		{
			_siteHudRoot.Visible = true;
			ApplySiteHudFullRect("battle_runtime_command_pause");
		}
		if (_sitePeacetimePanel != null)
		{ _sitePeacetimePanel.Visible = false; }
		if (_siteBottomCommandHost != null)
		{ _siteBottomCommandHost.Visible = true; }
		if (_battleRuntimeSummaryBar != null)
		{
			// Tactical pause is a read/command layer; the live summary strip stays hidden.
			_battleRuntimeSummaryBar.Visible = false;
		}
		if (_battleRuntimeCommandBar != null)
		{ _battleRuntimeCommandBar.Visible = false; }
		if (_battleRuntimePauseDetailPanel != null)
		{ _battleRuntimePauseDetailPanel.Visible = true; }
		RefreshBattleRuntimeHeroFrame();
		ApplyBattleMapOperationHudSuppressionVisibility("battle_runtime_command_pause");
		UpdateMainWorldViewportLayout("battle_runtime_command_pause");
	}

	private void RefreshBattleRuntimeHeroFrame()
	{
		BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
		bool hasRuntime = _activeBattleGroupRuntimeResolution?.RuntimeController != null &&
						  _activeBattleGroupRuntimeResolution.RuntimeController.IsComplete == false;
		bool hasReadySkill = HasReadyBattleRuntimeSkill(selected, hasRuntime);
		BattleEntity heroEntity = BuildBattleRuntimeHeroSkillSourceEntity(selected);
		IReadOnlyList<BattleSkillSnapshot> skills = BuildBattleRuntimeSkillSnapshots(selected);
		int runtimeSkillCount = _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions?.Count ?? 0;
		int readySkillCount = hasRuntime
			? skills.Count(skill =>
				skill != null &&
				ResolveSelectedHeroSkillUsageState(selected, ResolveSkillDefinitionId(skill)) == BattleRuntimeSkillUsageState.Ready)
			: 0;
		_battleRuntimeHeroFramePresenter.Refresh(
			selected,
			BuildBattleRuntimePlayerGroups(),
			hasRuntime,
			_battleRuntimeCommandPauseActive,
			hasReadySkill,
			heroEntity,
			skills,
			group => HasReadyBattleRuntimeSkill(group, hasRuntime),
			skillDefinitionId => ResolveSelectedHeroSkillUsageState(selected, skillDefinitionId));
		if (_battleRuntimeSummaryPresenter != null)
		{
			_battleRuntimeSummaryPresenter.Refresh(
				BuildBattleRuntimeHeroTroopSummaries(),
				selected?.GroupKey ?? _selectedBattleRuntimeGroupKey);
		}
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeHeroFrameRefreshed group={selected?.GroupKey ?? ""} runtimeSkills={runtimeSkillCount} filteredSkills={skills.Count} readySkills={readySkillCount} skillReady={hasReadySkill}");
	}
	private IReadOnlyList<BattleSkillSnapshot> BuildBattleRuntimeSkillSnapshots(BattleRuntimeCommandGroupView selected)
	{
		IReadOnlyList<BattleSkillSnapshot> runtimeSkills = _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions;
		return BattleRuntimeSkillFilter.FilterForGroup(runtimeSkills ?? System.Array.Empty<BattleSkillSnapshot>(), selected);
	}
	private bool HasReadyBattleRuntimeSkill(BattleRuntimeCommandGroupView selected, bool hasRuntime) =>
		selected != null &&
		hasRuntime &&
		BuildBattleRuntimeSkillSnapshots(selected).Any(skill =>
			skill != null &&
			ResolveSelectedHeroSkillUsageState(selected, ResolveSkillDefinitionId(skill)) == BattleRuntimeSkillUsageState.Ready);
	private bool IsSelectedHeroSkillUsedOrPending(BattleRuntimeCommandGroupView selected, string skillDefinitionId)
	{
		BattleRuntimeSkillUsageState state = ResolveSelectedHeroSkillUsageState(selected, skillDefinitionId);
		return state is BattleRuntimeSkillUsageState.Pending or BattleRuntimeSkillUsageState.Used;
	}
	private BattleRuntimeSkillUsageState ResolveSelectedHeroSkillUsageState(
		BattleRuntimeCommandGroupView selected,
		string skillDefinitionId)
	{
		return BattleRuntimeSkillUsageResolver.Resolve(
			selected,
			ResolveBattleRuntimeSkillSnapshot(selected, skillDefinitionId),
			_activeBattleGroupRuntimeResolution?.RuntimeController?.EventStream?.Events,
			_activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks,
			_activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0);
	}
	private BattleSkillSnapshot ResolveBattleRuntimeSkillSnapshot(BattleRuntimeCommandGroupView selected, string skillDefinitionId)
		=> BattleRuntimeSkillTargetingRules.ResolveSkillSnapshot(BuildBattleRuntimeSkillSnapshots(selected), _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions, skillDefinitionId);
	private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.ResolveSkillDefinitionId(skill);
	private static BattleSkillTargetingSnapshot ResolveTargeting(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.ResolveTargeting(skill);
	private static bool IsImmediateSelfSkill(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.IsImmediateSelfSkill(skill);
	private static bool UsesMarkThenLandingFlow(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.UsesMarkThenLandingFlow(skill);
	private static bool UsesDirectionAreaFlow(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.UsesDirectionAreaFlow(skill);
	private static int ResolveSkillRange(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.ResolveSkillRange(skill);
	private static int ResolveMarkLandingRadius(BattleSkillSnapshot skill) => BattleRuntimeSkillTargetingRules.ResolveMarkLandingRadius(skill);
	private void OnBattleRuntimeHeroSkillPressed()
	{
		BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
		string skillDefinitionId = ResolveSkillDefinitionId(BuildBattleRuntimeSkillSnapshots(selected).FirstOrDefault());
		BeginBattleRuntimeSkillPress(selected, skillDefinitionId);
	}
	private void OnBattleRuntimeSkillSlotPressed(string skillDefinitionId) =>
		BeginBattleRuntimeSkillPress(ResolveSelectedBattleRuntimeGroup(), skillDefinitionId);
	private void BeginBattleRuntimeSkillPress(BattleRuntimeCommandGroupView selected, string skillDefinitionId)
	{
		if (selected == null)
		{
			SetSiteNoticeText("请选择参战英雄。");
			return;
		}
		string normalizedSkillDefinitionId = (skillDefinitionId ?? "").Trim();
		if (string.IsNullOrWhiteSpace(normalizedSkillDefinitionId))
		{
			SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText("hero_skill_missing")}");
			RefreshBattleRuntimeHeroFrame();
			return;
		}

		IReadOnlyList<BattleSkillSnapshot> availableSkills = BuildBattleRuntimeSkillSnapshots(selected);
		BattleSkillSnapshot pressedSkill = availableSkills.FirstOrDefault(skill => string.Equals(ResolveSkillDefinitionId(skill), normalizedSkillDefinitionId, System.StringComparison.Ordinal));
		if (pressedSkill == null)
		{
			SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText("skill_caster_not_allowed")}");
			RefreshBattleRuntimeHeroFrame();
			return;
		}
		if (ResolveSelectedHeroSkillUsageState(selected, normalizedSkillDefinitionId) != BattleRuntimeSkillUsageState.Ready)
		{
			SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText(IsSelectedHeroSkillUsedOrPending(selected, normalizedSkillDefinitionId) ? "hero_skill_already_used" : "hero_actor_unavailable")}");
			RefreshBattleRuntimeHeroFrame();
			return;
		}
		if (!_battleRuntimeCommandPauseActive)
		{
			SetBattleRuntimeCommandPauseActive(true, "hero_skill_button");
		}
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeHeroSkillPressed group={selected.GroupKey} skill={normalizedSkillDefinitionId} pause={_battleRuntimeCommandPauseActive}");
		if (IsImmediateSelfSkill(pressedSkill))
		{
			CancelBattleRuntimeHeroSkillTargetPicking("self_skill_submit");
			_battleRuntimeHeroSkillTargetPickingGroup = selected;
			_battleRuntimeHeroSkillTargetPickingSkillDefinitionId = normalizedSkillDefinitionId;
			BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(selected);
			if (!TryResolveBattleRuntimeHeroSkillSourceActorId(source, out string sourceActorId)) { SetSiteNoticeText("技能暂不可用：当前英雄无法行动。"); RefreshBattleRuntimeHeroFrame(); return; }
			SubmitBattleRuntimeHeroSkillCommand(selected, sourceActorId, "");
			return;
		}
		BeginBattleRuntimeHeroSkillTargetPicking(selected, normalizedSkillDefinitionId);
	}
	private void BeginBattleRuntimeHeroSkillTargetPicking(BattleRuntimeCommandGroupView selected, string skillDefinitionId)
	{
		if (selected == null ||
			_activeBattleGroupRuntimeResolution?.RuntimeController == null ||
			_activeBattleGroupRuntimeResolution.RuntimeController.IsComplete)
		{
			SetSiteNoticeText("技能暂不可用：战斗运行时尚未准备好。");
			RefreshBattleRuntimeHeroFrame();
			return;
		}
		_battleRuntimeHeroSkillTargetPickingActive = true;
		_battleRuntimeHeroSkillTargetPickingGroup = selected;
		string normalizedSkillDefinitionId = (skillDefinitionId ?? "").Trim();
		if (string.IsNullOrWhiteSpace(normalizedSkillDefinitionId))
		{
			SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText("hero_skill_missing")}");
			RefreshBattleRuntimeHeroFrame();
			return;
		}

		_battleRuntimeHeroSkillTargetPickingSkillDefinitionId = normalizedSkillDefinitionId;
		_battleRuntimeHeroSkillPreviewTargetActorId = "";
		BattleSkillSnapshot pickedSkill = ResolveBattleRuntimeSkillSnapshot(selected, normalizedSkillDefinitionId);
		_battleRuntimeSkillTargetingStage = UsesMarkThenLandingFlow(pickedSkill)
			? SkillTargetingStage.PrimarySelection
			: SkillTargetingStage.None;
		_battleRuntimeSelectedRuntimeAnchorId = "";
		_battleRuntimeSelectedRuntimeAnchorSurface = default;
		EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget, "runtime_skill_target_start");
		RefreshBattleRuntimeHeroSkillTargetPreview();
		SetSiteNoticeText(_battleRuntimeSkillTargetingStage == SkillTargetingStage.PrimarySelection
			? $"{selected.DisplayName}：选择一个已有雷印。"
			: $"{selected.DisplayName}：选择一个敌方目标释放技能。");
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeHeroSkillTargetPickingStarted group={selected.GroupKey} skill={_battleRuntimeHeroSkillTargetPickingSkillDefinitionId}");
	}
	private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)
	{
		if (!_battleRuntimeHeroSkillTargetPickingActive)
		{
			return false;
		}
		if (inputEvent is InputEventMouseMotion)
		{
			RefreshBattleRuntimeHeroSkillTargetPreview();
			return false;
		}
		if (inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return false;
		}
		if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			CancelBattleRuntimeHeroSkillTargetPicking("cancel_click");
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		if (mouseButton.ButtonIndex != MouseButton.Left)
		{
			return false;
		}
		RefreshBattleRuntimeHeroSkillTargetPreview();
		if (!TryGetMouseGridPosition(out GridPosition position))
		{
			SetSiteNoticeText("请选择战场内的敌方目标。");
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
		if (!TryResolveBattleRuntimeHeroSkillSourceActorId(source, out string sourceActorId))
		{
			SetSiteNoticeText("技能暂不可用：当前英雄无法行动。");
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		GridPosition previewAnchor = ResolveBattleRuntimeHeroSkillPreviewAnchor(source);
		BattleSkillSnapshot pickedSkill = BuildBattleRuntimeSkillSnapshots(_battleRuntimeHeroSkillTargetPickingGroup)
			.FirstOrDefault(item => string.Equals(ResolveSkillDefinitionId(item), _battleRuntimeHeroSkillTargetPickingSkillDefinitionId, System.StringComparison.Ordinal))
			?? _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions?
				.FirstOrDefault(item => string.Equals(ResolveSkillDefinitionId(item), _battleRuntimeHeroSkillTargetPickingSkillDefinitionId, System.StringComparison.Ordinal));
		BattleEntity target = FindEntityAt(position);
		if (UsesMarkThenLandingFlow(pickedSkill))
		{
			if (_battleRuntimeSkillTargetingStage == SkillTargetingStage.SecondarySelection)
			{
				TrySubmitBattleRuntimeMarkLanding(position, sourceActorId, pickedSkill);
			}
			else if (TrySelectBattleRuntimeMark(position, target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition markSurface))
			{
				BeginBattleRuntimeMarkLandingSelection(mark, markSurface);
			}
			else
			{
				SetSiteNoticeText("雷印折跃需要先选择已有雷印。");
			}
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		if (UsesDirectionAreaFlow(pickedSkill))
		{
			TrySubmitBattleRuntimeDirectionalArea(position, sourceActorId, pickedSkill);
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		if (pickedSkill?.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell)
		{
			if (TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorOrCellId) &&
				IsBattleRuntimeHeroSkillTargetInRange(source, previewAnchor, target, pickedSkill.Range))
			{
				SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorOrCellId);
			}
			else if (IsBattleRuntimeHeroSkillCellInRange(source, previewAnchor, position, pickedSkill.Range))
			{
				SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position);
			}
			else
			{
				SetSiteNoticeText("请选择技能范围内的目标。");
				RefreshBattleRuntimeHeroSkillTargetPreview();
			}
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		if (pickedSkill?.TargetingMode == BattleSkillTargetingMode.TargetedCell)
		{
			if (!IsBattleRuntimeHeroSkillCellInRange(source, previewAnchor, position, pickedSkill.Range))
			{
				SetSiteNoticeText("请选择技能范围内的地块。");
				RefreshBattleRuntimeHeroSkillTargetPreview();
				GetViewport()?.SetInputAsHandled();
				return true;
			}

			SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position);
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		if (!TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorId) ||
			!IsBattleRuntimeHeroSkillTargetInRange(source, previewAnchor, target, pickedSkill?.Range ?? 0))
		{
			SetSiteNoticeText("请选择可被技能影响的敌方单位。");
			GetViewport()?.SetInputAsHandled();
			return true;
		}
		SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorId);
		GetViewport()?.SetInputAsHandled();
		return true;
	}
	private void SubmitBattleRuntimeHeroSkillCommand(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId, GridPosition? targetGrid = null, string selectedSpatialMarkId = "")
	{
		CommandRequest commandRequest = targetGrid.HasValue
			? string.IsNullOrWhiteSpace(selectedSpatialMarkId)
				? BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, targetGrid)
				: BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, targetGrid, selectedSpatialMarkId)
			: string.IsNullOrWhiteSpace(selectedSpatialMarkId)
				? BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId)
				: BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, null, selectedSpatialMarkId);
		BattleCommandSubmissionResult result = new BattleCommandSubmissionService().Submit(
			_activeBattleGroupRuntimeResolution?.Snapshot,
			StrategicWorldRuntime.State?.PlayerFactionId ?? "",
			commandRequest,
			_activeBattleGroupRuntimeResolution?.RuntimeController);
		bool accepted = result?.Accepted == true;
		if (accepted)
		{
			SetSiteNoticeText($"{selected?.DisplayName ?? "参战英雄"}：英雄技能已下达，恢复战斗后生效。");
		}
		else
		{
			SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText(result?.ReasonCode)}");
		}
		if (accepted)
		{
			CancelBattleRuntimeHeroSkillTargetPicking("submitted");
		}
		else
		{
			RefreshBattleRuntimeHeroSkillTargetPreview();
		}
		RefreshBattleRuntimeHeroFrame();
		GameLog.Info(
			nameof(WorldSiteRoot),
			$"BattleRuntimeHeroSkillSubmitted group={commandRequest.BattleGroupId} source={commandRequest.SourceActorId} skill={commandRequest.SkillDefinitionId} target={commandRequest.TargetActorId} accepted={result?.Accepted == true} reason={result?.ReasonCode ?? "runtime_missing"} events={result?.Events.Count ?? 0}");
	}
	private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId, GridPosition? targetGrid = null, string selectedSpatialMarkId = "")
	{
		string groupKey = selected?.GroupKey ?? _selectedBattleRuntimeGroupKey ?? "";
		string battleId = _activeBattleGroupRuntimeResolution?.RuntimeController?.BattleId ?? _battleRuntimeRequest?.ContextId ?? "";
		string skillDefinitionId = (_battleRuntimeHeroSkillTargetPickingSkillDefinitionId ?? "").Trim();
		return BattleRuntimeHeroSkillCommandRequestFactory.BuildHeroSkillCommandRequest(groupKey, battleId, skillDefinitionId, sourceActorId, targetActorId, targetGrid, selectedSpatialMarkId);
	}
	private void RefreshBattleRuntimeHeroSkillTargetPreview()
	{
		if (!_battleRuntimeHeroSkillTargetPickingActive)
		{
			return;
		}
		BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
		BattleSkillSnapshot pickedSkill = ResolveBattleRuntimeSkillSnapshot(
			_battleRuntimeHeroSkillTargetPickingGroup,
			_battleRuntimeHeroSkillTargetPickingSkillDefinitionId);
		GridPosition previewAnchor = ResolveBattleRuntimeHeroSkillPreviewAnchor(source);
		IReadOnlyList<GridPosition> rangeCells = BuildBattleRuntimeHeroSkillRangeCells(source, previewAnchor);
		IReadOnlyList<GridPosition> targetCells = System.Array.Empty<GridPosition>();
		string targetActorId = "";
		bool usesMarkThenLanding = UsesMarkThenLandingFlow(pickedSkill);
		bool usesDirectionArea = UsesDirectionAreaFlow(pickedSkill);
		if (usesMarkThenLanding && _battleRuntimeSkillTargetingStage == SkillTargetingStage.PrimarySelection) { rangeCells = BuildBattleRuntimeMarkCandidateCells(); }
		else if (usesMarkThenLanding && _battleRuntimeSkillTargetingStage == SkillTargetingStage.SecondarySelection) { rangeCells = BuildBattleRuntimeMarkLandingCells(source, pickedSkill); }
		if (TryGetMouseGridPosition(out GridPosition position))
		{
			if (usesDirectionArea)
			{
				targetCells = BuildBattleRuntimeDirectionalAreaCells(source, previewAnchor, position, pickedSkill);
			}
			else if (usesMarkThenLanding && _battleRuntimeSkillTargetingStage == SkillTargetingStage.SecondarySelection)
			{
				targetCells = rangeCells.Contains(position) ? new[] { position } : System.Array.Empty<GridPosition>();
			}
			else
			{
				BattleEntity target = FindEntityAt(position);
				if (usesMarkThenLanding && _battleRuntimeSkillTargetingStage == SkillTargetingStage.PrimarySelection)
				{
					if (TrySelectBattleRuntimeMark(position, target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition markSurface))
					{
						targetActorId = string.IsNullOrWhiteSpace(mark?.AttachedActorId) ? "" : mark.AttachedActorId;
						targetCells = string.IsNullOrWhiteSpace(targetActorId)
							? new[] { new GridPosition(markSurface.X, markSurface.Y) }
							: BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(target);
					}
				}
				else if (TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out targetActorId) &&
						 IsBattleRuntimeHeroSkillTargetInRange(source, previewAnchor, target, ResolveBattleRuntimeHeroSkillRange()))
				{
					targetCells = BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(target);
				}
			}
		}
		_battleRuntimeHeroSkillPreviewTargetActorId = targetActorId;
		_highlightOverlay?.SetCellsBatch(
			(BattleGridHighlightKind.Skill, rangeCells),
			(BattleGridHighlightKind.Target, targetCells));
		_unitRoot?.SetAttackTargetPreviewByEntityId(targetActorId);
	}
	private void CancelBattleRuntimeHeroSkillTargetPicking(string reason)
	{
		if (!_battleRuntimeHeroSkillTargetPickingActive &&
			string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillPreviewTargetActorId))
		{
			return;
		}
		_battleRuntimeHeroSkillTargetPickingActive = false;
		_battleRuntimeHeroSkillTargetPickingGroup = null;
		_battleRuntimeHeroSkillTargetPickingSkillDefinitionId = "";
		_battleRuntimeHeroSkillPreviewTargetActorId = "";
		_battleRuntimeSkillTargetingStage = SkillTargetingStage.None;
		_battleRuntimeSelectedRuntimeAnchorId = "";
		_battleRuntimeSelectedRuntimeAnchorSurface = default;
		_highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill);
		_highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
		_unitRoot?.ClearAttackTargetPreview();
		ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget, reason);
		GameLog.Info(nameof(WorldSiteRoot), $"BattleRuntimeHeroSkillTargetPickingCancelled reason={reason ?? ""}");
	}
	private bool TrySelectBattleRuntimeMark(GridPosition position, BattleEntity target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition surface) => BattleRuntimeMarkTargetingPresentation.TrySelectMark(_activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks, _battleRuntimeHeroSkillTargetPickingGroup?.GroupKey ?? _selectedBattleRuntimeGroupKey, _activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0, position, target, out mark, out surface);
	private void BeginBattleRuntimeMarkLandingSelection(BattleRuntimeSpatialMark mark, GridSurfacePosition surface)
	{
		_battleRuntimeSkillTargetingStage = SkillTargetingStage.SecondarySelection; _battleRuntimeSelectedRuntimeAnchorId = mark?.MarkId ?? ""; _battleRuntimeSelectedRuntimeAnchorSurface = surface; _battleRuntimeHeroSkillPreviewTargetActorId = ""; RefreshBattleRuntimeHeroSkillTargetPreview();
		SetSiteNoticeText("雷印已选定：请选择雷印周围3格内的空地。");
	}
	private IReadOnlyList<GridPosition> BuildBattleRuntimeMarkCandidateCells() => BattleRuntimeMarkTargetingPresentation.BuildMarkCells(_activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks, _battleRuntimeHeroSkillTargetPickingGroup?.GroupKey ?? _selectedBattleRuntimeGroupKey, _activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0, _unitRoot);
	private IReadOnlyList<GridPosition> BuildBattleRuntimeMarkLandingCells(BattleEntity source, BattleSkillSnapshot skill) =>
		BattleRuntimeMarkTargetingPresentation.BuildLandingCells(_activeGridMap, _unitRoot, source, _battleRuntimeSelectedRuntimeAnchorSurface, ResolveMarkLandingRadius(skill));
	private IReadOnlyList<GridPosition> BuildBattleRuntimeDirectionalAreaCells(BattleEntity source, GridPosition sourceAnchor, GridPosition position, BattleSkillSnapshot skill) =>
		BattleRuntimeHeroSkillTargetPresentation.BuildAreaPreviewCells(ResolveTargeting(skill), source, sourceAnchor, position, _activeGridMap);
	private bool ResolveBattleRuntimeDirectionalAreaCenter(BattleEntity source, GridPosition sourceAnchor, GridPosition position, BattleSkillSnapshot skill, out GridPosition center) =>
		BattleRuntimeHeroSkillTargetPresentation.TryResolveDirectionalAreaCenter(ResolveTargeting(skill), source, sourceAnchor, position, out center);
	private void TrySubmitBattleRuntimeDirectionalArea(GridPosition position, string sourceActorId, BattleSkillSnapshot skill)
	{
		BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
		GridPosition previewAnchor = ResolveBattleRuntimeHeroSkillPreviewAnchor(source);
		if (!ResolveBattleRuntimeDirectionalAreaCenter(source, previewAnchor, position, skill, out GridPosition center) ||
			!IsBattleRuntimeHeroSkillCellInRange(source, previewAnchor, center, ResolveBattleRuntimeHeroSkillRange()))
		{
			SetSiteNoticeText("雷旋破：请选择英雄周围的释放方向。");
			RefreshBattleRuntimeHeroSkillTargetPreview();
			return;
		}

		// HUD submits the resolved area center; Runtime owns damage, lock timing, and hit validation.
		SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", center);
	}
	private void TrySubmitBattleRuntimeMarkLanding(GridPosition position, string sourceActorId, BattleSkillSnapshot skill)
	{
		BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
		if (!BuildBattleRuntimeMarkLandingCells(source, skill).Contains(position))
		{
			SetSiteNoticeText("请选择雷印周围3格内的空地。");
			RefreshBattleRuntimeHeroSkillTargetPreview();
			return;
		}
		// The first click selects the Runtime mark; only this second legal landing click submits command intent.
		SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position, _battleRuntimeSelectedRuntimeAnchorId);
	}
	private bool TryResolveBattleRuntimeHeroSkillTargetActorId(BattleEntity source, BattleEntity target, out string targetActorId) => BattleRuntimeHeroSkillTargetPresentation.TryResolveTargetActorId(source, target, out targetActorId);
	private bool TryResolveBattleRuntimeHeroSkillSourceActorId(BattleEntity source, out string sourceActorId) => BattleRuntimeHeroSkillTargetPresentation.TryResolveSourceActorId(source, out sourceActorId);
	// Local picking mirrors the drawn preview; Runtime remains the command authority.
	private bool IsBattleRuntimeHeroSkillTargetInRange(BattleEntity source, GridPosition sourceAnchor, BattleEntity target, int range) => BattleRuntimeHeroSkillTargetPresentation.IsTargetInRange(source, sourceAnchor, target, range);
	private bool IsBattleRuntimeHeroSkillCellInRange(BattleEntity source, GridPosition sourceAnchor, GridPosition position, int range) => BattleRuntimeHeroSkillTargetPresentation.IsCellInRange(source, sourceAnchor, position, range);
	private BattleEntity BuildBattleRuntimeHeroSkillSourceEntity(BattleRuntimeCommandGroupView selected) => BattleRuntimeHeroSkillTargetPresentation.ResolveSourceEntity(_unitRoot, selected?.Forces);
	private GridPosition ResolveBattleRuntimeHeroSkillPreviewAnchor(BattleEntity source)
	{
		if (_unitRoot?.TryResolveMovementPreviewSurface(source, out GridSurfacePosition movementSurface) == true)
		{
			return movementSurface.Position;
		}

		return source?.GetComponent<GridOccupantComponent>()?.Position ?? default;
	}
	private IReadOnlyList<GridPosition> BuildBattleRuntimeHeroSkillRangeCells(BattleEntity source, GridPosition sourceAnchor) => BattleRuntimeHeroSkillTargetPresentation.BuildRangeCells(source, sourceAnchor, _activeGridMap, ResolveBattleRuntimeHeroSkillRange());
	private int ResolveBattleRuntimeHeroSkillRange()
	{
		string skillDefinitionId = (_battleRuntimeHeroSkillTargetPickingSkillDefinitionId ?? "").Trim();
		if (string.IsNullOrWhiteSpace(skillDefinitionId))
		{
			return 0;
		}

		return ResolveSkillRange(ResolveBattleRuntimeSkillSnapshot(_battleRuntimeHeroSkillTargetPickingGroup, skillDefinitionId));
	}
	private IReadOnlyList<BattleRuntimeCommandGroupView> BuildBattleRuntimePlayerGroups() =>
		BattleRuntimeCommandGroupSelection.BuildPlayerGroups(
			_battleRuntimeRequest ?? (TryResolveActiveBattleRequest(out BattleStartRequest activeRequest) ? activeRequest : null),
			_battleUnitFactory.ResolveUnitDisplayName);

	private IReadOnlyList<BattleRuntimeHeroTroopSummaryView> BuildBattleRuntimeHeroTroopSummaries() =>
		BattleRuntimeCommandGroupSelection.BuildHeroTroopSummaries(BuildBattleRuntimePlayerGroups(), _activeBattleGroupRuntimeResolution?.RuntimeController?.State, BuildBattleRuntimeHeroSkillSourceEntity);

	private BattleRuntimeCommandGroupView ResolveSelectedBattleRuntimeGroup() =>
		BattleRuntimeCommandGroupSelection.ResolveSelected(
			BuildBattleRuntimePlayerGroups(),
			ref _selectedBattleRuntimeGroupKey,
			_selectedBattleRuntimeGroupKeys);

}
