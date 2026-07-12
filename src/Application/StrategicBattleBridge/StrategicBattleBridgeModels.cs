using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Runtime.Battle;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattleSessionResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public StrategicBattleSession Session { get; set; } = new();

    public static StrategicBattleSessionResult Ok(StrategicBattleSession session)
    {
        return new StrategicBattleSessionResult
        {
            Success = true,
            Session = session ?? new StrategicBattleSession()
        };
    }

    public static StrategicBattleSessionResult Failed(string reason)
    {
        return new StrategicBattleSessionResult
        {
            Success = false,
            FailureReason = reason ?? ""
        };
    }
}

public sealed class StrategicBattleSnapshotResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public BattleStartSnapshot Snapshot { get; set; } = new();

    public static StrategicBattleSnapshotResult Ok(BattleStartSnapshot snapshot)
    {
        return new StrategicBattleSnapshotResult
        {
            Success = true,
            Snapshot = snapshot ?? new BattleStartSnapshot()
        };
    }

    public static StrategicBattleSnapshotResult Failed(string reason)
    {
        return new StrategicBattleSnapshotResult
        {
            Success = false,
            FailureReason = reason ?? ""
        };
    }
}

public sealed class StrategicBattleActiveContextResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public StrategicBattleActiveContext Context { get; set; } = new();

    public static StrategicBattleActiveContextResult Ok(StrategicBattleActiveContext context)
    {
        return new StrategicBattleActiveContextResult
        {
            Success = true,
            Context = context ?? new StrategicBattleActiveContext()
        };
    }

    public static StrategicBattleActiveContextResult Failed(string reason)
    {
        return new StrategicBattleActiveContextResult
        {
            Success = false,
            FailureReason = reason ?? ""
        };
    }
}

public sealed class StrategicBattleActiveContextToken : System.IEquatable<StrategicBattleActiveContextToken>
{
    public StrategicBattleActiveContextToken(
        string contextId,
        string sessionId,
        string snapshotId,
        long revision,
        string resultId = "")
    {
        ContextId = contextId ?? "";
        SessionId = sessionId ?? "";
        SnapshotId = snapshotId ?? "";
        Revision = revision;
        ResultId = resultId ?? "";
    }

    public string ContextId { get; }
    public string SessionId { get; }
    public string SnapshotId { get; }
    public long Revision { get; }
    public string ResultId { get; }

    public bool Equals(StrategicBattleActiveContextToken other)
    {
        return other != null &&
               string.Equals(ContextId, other.ContextId, System.StringComparison.Ordinal) &&
               string.Equals(SessionId, other.SessionId, System.StringComparison.Ordinal) &&
               string.Equals(SnapshotId, other.SnapshotId, System.StringComparison.Ordinal) &&
               Revision == other.Revision &&
               string.Equals(ResultId, other.ResultId, System.StringComparison.Ordinal);
    }

    public override bool Equals(object obj) => Equals(obj as StrategicBattleActiveContextToken);

    public override int GetHashCode() => System.HashCode.Combine(
        ContextId,
        SessionId,
        SnapshotId,
        Revision,
        ResultId);

    public override string ToString() =>
        $"context={ContextId} session={SessionId} snapshot={SnapshotId} revision={Revision} result={ResultId}";
}

public sealed class StrategicBattleResultEnvelope
{
    internal StrategicBattleResultEnvelope(
        string resultId,
        string sessionId,
        string snapshotId,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report)
    {
        ResultId = resultId ?? "";
        SessionId = sessionId ?? "";
        SnapshotId = snapshotId ?? "";
        RuntimeResult = runtimeResult;
        SettlementPlan = settlementPlan;
        Report = report;
    }

    public string ResultId { get; }
    public string SessionId { get; }
    public string SnapshotId { get; }
    public BattleRuntimeSessionResult RuntimeResult { get; }
    public SettlementPlan SettlementPlan { get; }
    public BattleReportRecord Report { get; }

    internal static StrategicBattleResultEnvelope Create(
        string sessionId,
        string snapshotId,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report)
    {
        string resultId = ComputeResultId(
            sessionId,
            snapshotId,
            runtimeResult,
            settlementPlan,
            report);
        return new StrategicBattleResultEnvelope(
            resultId,
            sessionId,
            snapshotId,
            runtimeResult,
            settlementPlan,
            report);
    }

    internal bool HasIntactIdentity()
    {
        try
        {
            return string.Equals(
                ResultId,
                ComputeResultId(SessionId, SnapshotId, RuntimeResult, SettlementPlan, Report),
                System.StringComparison.Ordinal);
        }
        catch (System.Exception)
        {
            // Mutable producer objects must never change accepted result facts
            // without invalidating the result token's canonical digest.
            return false;
        }
    }

    private static string ComputeResultId(
        string sessionId,
        string snapshotId,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report)
    {
        string identityPayload = JsonSerializer.Serialize(new
        {
            SessionId = sessionId ?? "",
            SnapshotId = snapshotId ?? "",
            Outcome = runtimeResult?.Outcome,
            Events = runtimeResult?.EventStream?.Events,
            FinalState = runtimeResult?.FinalState,
            SettlementPlan = settlementPlan,
            Report = report
        });
        return System.Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identityPayload)))
            .ToLowerInvariant();
    }
}

public sealed class StrategicBattleActiveContext
{
    private readonly object _resultEnvelopeGate = new();
    private StrategicBattleResultEnvelope _resultEnvelope;

    public string ContextId { get; set; } = "";
    public string ScenePath { get; set; } = "";
    public string ReturnScenePath { get; set; } = "";
    public StrategicBattleSession Session { get; set; } = new();
    public StrategicBattlePreparationDraft PreparationDraft { get; set; }
    public BattleStartSnapshot PreparationSeedSnapshot { get; set; } = new();
    public string PreparationDraftId { get; set; } = "";
    public long PreparationDraftRevision { get; set; }
    public string FinalizedDraftId { get; set; } = "";
    public long FinalizedDraftRevision { get; set; }
    public BattleStartSnapshot Snapshot { get; set; } = new();
    // Created only after the Draft compiles the final Snapshot. This projection
    // is outbound compatibility state and never participates in compilation.
    public BattleStartRequest CompatibilityRequest { get; set; }
    public StrategicBattleResultEnvelope ResultEnvelope
    {
        get
        {
            lock (_resultEnvelopeGate)
            {
                return _resultEnvelope;
            }
        }
    }
    public BattleResult CompatibilityResult { get; set; }
    public bool ResultConsumed { get; internal set; }
    public string FailureReason { get; set; } = "";

    internal bool TryAcceptResultEnvelope(StrategicBattleResultEnvelope resultEnvelope)
    {
        lock (_resultEnvelopeGate)
        {
            if (_resultEnvelope != null)
            {
                return false;
            }

            _resultEnvelope = resultEnvelope;
            return true;
        }
    }
}

public sealed class StrategicBattleSession
{
    public string SessionId { get; set; } = "";
    public string ExpeditionId { get; set; } = "";
    public string SourceLocationId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public string AttackerFactionId { get; set; } = "";
    public string DefenderFactionId { get; set; } = "";
    public BattleKind BattleKind { get; set; } = BattleKind.Unknown;
    public string EncounterId { get; set; } = "";
    public string MapDefinitionId { get; set; } = "";
    public string BattleObjectiveId { get; set; } = "";
    public string ReturnScenePath { get; set; } = "";
    public string SiteScenePath { get; set; } = "";
    public List<StrategicBattleParticipantReference> Participants { get; set; } = new();
}

public sealed class StrategicBattleParticipantReference
{
    public string ParticipantId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceLocationId { get; set; } = "";
    public string RollbackStationLocationId { get; set; } = "";
    public StrategicBattleParticipantRole Role { get; set; } = StrategicBattleParticipantRole.Unknown;
    public int PreBattleCorpsStrength { get; set; }
    public int CorpsLevel { get; set; }
    public int CorpsEquipmentLevel { get; set; }
}

public sealed class StrategicBattleResultSummary
{
    public string SessionId { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string ExpeditionId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public BattleOutcome Outcome { get; set; } = BattleOutcome.None;
    public bool ObjectiveSucceeded { get; set; }
    public List<StrategicBattleParticipantResult> Participants { get; set; } = new();
    public List<StrategicBattleParticipantDisposition> ParticipantDispositions { get; set; } = new();
    public bool HasConsequenceFacts { get; set; }
    public string TargetDisplayName { get; set; } = "";
    public string WorldChangeText { get; set; } = "";
    public string FailureReasonText { get; set; } = "";
    public string ProgressionText { get; set; } = "";
    public string RewardClaimId { get; set; } = "";
    public List<string> RewardLines { get; set; } = new();
    public List<StrategicResourceAmount> ResourceRewards { get; set; } = new();
    public List<string> RewardEquipmentSampleIds { get; set; } = new();
}

public sealed class StrategicBattleParticipantDisposition
{
    public string ParticipantId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string RollbackStationLocationId { get; set; } = "";
    public StrategicBattleParticipantRole Role { get; set; } = StrategicBattleParticipantRole.Unknown;
}

public sealed class StrategicBattleParticipantResult
{
    public string HeroId { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public int RemainingCorpsStrength { get; set; }
}
