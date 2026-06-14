using System.Collections.Generic;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicManagementState
{
    public int ElapsedWorldTimePulses { get; set; }
    public Dictionary<string, StrategicFactionResourceStore> FactionResources { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicLocationState> Locations { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicCityState> Cities { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicCorpsInstanceState> CorpsInstances { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicHeroState> Heroes { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicExpeditionState> Expeditions { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicBattleFeedbackRecord> BattleFeedbackRecords { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, string> BattleFeedbackRecordIdsByExpedition { get; set; } =
        new(System.StringComparer.Ordinal);

    public List<string> UnlockedEquipmentSampleIds { get; set; } = new();

    public List<string> ClaimedBattleRewardIds { get; set; } = new();

    public int NextCorpsSerial { get; set; } = 1;
    public int NextExpeditionSerial { get; set; } = 1;
    public int NextBattleFeedbackSerial { get; set; } = 1;

    public int GetResourceAmount(string factionId, string resourceId)
    {
        return FactionResources.TryGetValue(factionId ?? "", out StrategicFactionResourceStore store)
            ? store.Get(resourceId)
            : 0;
    }

    public void SetResourceAmount(string factionId, string resourceId, int amount)
    {
        EnsureResourceStore(factionId).Set(resourceId, amount);
    }

    public void AddResourceAmount(string factionId, string resourceId, int amount)
    {
        EnsureResourceStore(factionId).Add(resourceId, amount);
    }

    public bool CanSpend(string factionId, IReadOnlyCollection<StrategicResourceAmount> costs)
    {
        foreach (StrategicResourceAmount cost in costs ?? System.Array.Empty<StrategicResourceAmount>())
        {
            if (cost.Amount <= 0)
            {
                continue;
            }

            if (!FactionResources.TryGetValue(factionId ?? "", out StrategicFactionResourceStore store) ||
                !store.CanSpend(cost.ResourceId, cost.Amount))
            {
                return false;
            }
        }

        return true;
    }

    public void Spend(string factionId, IReadOnlyCollection<StrategicResourceAmount> costs)
    {
        StrategicFactionResourceStore store = EnsureResourceStore(factionId);
        foreach (StrategicResourceAmount cost in costs ?? System.Array.Empty<StrategicResourceAmount>())
        {
            store.Spend(cost.ResourceId, cost.Amount);
        }
    }

    public string AllocateCorpsInstanceId()
    {
        string id = $"corps_{NextCorpsSerial:0000}";
        NextCorpsSerial++;
        return id;
    }

    public string AllocateExpeditionId()
    {
        string id = $"expedition_{NextExpeditionSerial:0000}";
        NextExpeditionSerial++;
        return id;
    }

    public string AllocateBattleFeedbackId()
    {
        string id = $"battle_feedback_{NextBattleFeedbackSerial:0000}";
        NextBattleFeedbackSerial++;
        return id;
    }

    private StrategicFactionResourceStore EnsureResourceStore(string factionId)
    {
        string key = factionId ?? "";
        if (!FactionResources.TryGetValue(key, out StrategicFactionResourceStore store))
        {
            store = new StrategicFactionResourceStore { FactionId = key };
            FactionResources[key] = store;
        }

        return store;
    }
}

public sealed class StrategicBattleFeedbackRecord
{
    public string FeedbackId { get; set; } = "";
    public string ExpeditionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public string TargetDisplayName { get; set; } = "";
    public bool Victory { get; set; }
    public bool ObjectiveSucceeded { get; set; }
    public string OutcomeText { get; set; } = "";
    public string WorldChangeText { get; set; } = "";
    public string FailureReasonText { get; set; } = "";
    public string ProgressionText { get; set; } = "";
    public int AppliedElapsedWorldTimePulses { get; set; }
    public List<string> RewardLines { get; set; } = new();
    public List<StrategicBattleParticipantFeedbackRecord> ParticipantFeedback { get; set; } = new();
    public List<StrategicHeroBattleFeedbackRecord> HeroFeedback { get; set; } = new();
    public List<StrategicEquipmentSampleFeedbackRecord> EquipmentSamples { get; set; } = new();
}

public sealed class StrategicBattleParticipantFeedbackRecord
{
    public string HeroId { get; set; } = "";
    public string HeroDisplayName { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDisplayName { get; set; } = "";
    public int RemainingCorpsStrength { get; set; }
    public int StrengthLoss { get; set; }
    public string ResultText { get; set; } = "";
}

public sealed class StrategicHeroBattleFeedbackRecord
{
    public string HeroId { get; set; } = "";
    public string HeroDisplayName { get; set; } = "";
    public string ReactionText { get; set; } = "";
}

public sealed class StrategicEquipmentSampleFeedbackRecord
{
    public string EquipmentSampleId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SlotKind { get; set; } = "";
    public string Grade { get; set; } = "";
    public string RoleText { get; set; } = "";
    public bool IsReward { get; set; }
}

public sealed class StrategicFactionResourceStore
{
    public string FactionId { get; set; } = "";
    public Dictionary<string, int> Amounts { get; set; } = new(System.StringComparer.Ordinal);

    public int Get(string resourceId)
    {
        return !string.IsNullOrWhiteSpace(resourceId) && Amounts.TryGetValue(resourceId, out int value)
            ? value
            : 0;
    }

    public void Set(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return;
        }

        Amounts[resourceId] = System.Math.Max(0, amount);
    }

    public void Add(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount == 0)
        {
            return;
        }

        Set(resourceId, Get(resourceId) + amount);
    }

    public bool CanSpend(string resourceId, int amount)
    {
        return amount <= 0 || Get(resourceId) >= amount;
    }

    public bool Spend(string resourceId, int amount)
    {
        if (!CanSpend(resourceId, amount))
        {
            return false;
        }

        Add(resourceId, -amount);
        return true;
    }
}
