using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public static class BattleTacticalIntentIds
{
    public const string AssaultTarget = "AssaultTarget";
    public const string DefendTarget = "DefendTarget";
    public const string SallyOut = "SallyOut";
    public const string HoldLine = "HoldLine";
    public const string HarassAndReturn = "HarassAndReturn";
    public const string ProtectTarget = "ProtectTarget";
    public const string RetreatToTarget = "RetreatToTarget";
}

public static class BattleTargetSelectors
{
    public const string CurrentSelectedRegion = "CurrentSelectedRegion";
    public const string PlayerDeploymentRegion = "PlayerDeploymentRegion";
    public const string RuntimeObservedHostileCluster = "RuntimeObservedHostileCluster";
}

public static class BattleRetargetPolicyIds
{
    public const string StableUntilInvalid = "StableUntilInvalid";
    public const string AllowVolatileObservation = "AllowVolatileObservation";
}

public static class BattleTacticalIntentPlanSources
{
    public const string ExplicitGroup = "ExplicitGroup";
    public const string ForceDefault = "ForceDefault";
    public const string ScenarioDefault = "ScenarioDefault";
    public const string Snapshot = "Snapshot";
    public const string SafeFallback = "SafeFallback";
}

public sealed class BattleTacticalIntentPlanSnapshot
{
    public string IntentId { get; set; } = "";
    public string PrimaryTargetSelector { get; set; } = "";
    public List<string> SecondaryTargetSelectors { get; set; } = new();
    public string StyleProfileId { get; set; } = "";
    public string LeashSelector { get; set; } = "";
    public string RetargetPolicyId { get; set; } = "";
    public string EngagementPolicyId { get; set; } = "";
    public string FallbackIntentId { get; set; } = "";
    public string IntentSource { get; set; } = "";
}
