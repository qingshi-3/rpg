using System;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;

namespace Rpg.Infrastructure.Scenes;

public sealed class SceneTransitionSiteVisitRequest
{
    public string SiteId { get; set; } = "";
    public string TargetScenePath { get; set; } = "";
    public string ReturnScenePath { get; set; } = "";
    public string ArmyId { get; set; } = "";
    public Action OnEntered { get; set; }
}

public sealed class SceneTransitionBattleRequest
{
    public BattleStartRequest Request { get; set; }
    public StrategicBattleActiveContext ActiveContext { get; set; }
    public Action OnSuccess { get; set; }
    public Action<string> RollbackOnFailure { get; set; }
}

public sealed class SceneTransitionReturnRequest
{
    public string TargetScenePath { get; set; } = "";
    public bool MarkWorldResume { get; set; } = true;
    public Action OnEntered { get; set; }
}
