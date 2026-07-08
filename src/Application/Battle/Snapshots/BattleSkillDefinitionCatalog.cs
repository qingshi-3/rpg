using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Rpg.Application.Config;
using Rpg.Definitions.Battle.Skills;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillDefinitionCatalog
{
    public BattleSkillDefinitionCatalog(IReadOnlyDictionary<string, BattleSkillDefinitionResource> definitionsById)
        : this(definitionsById, new Dictionary<string, BattleSkillSnapshot>(StringComparer.Ordinal))
    {
    }

    private BattleSkillDefinitionCatalog(
        IReadOnlyDictionary<string, BattleSkillDefinitionResource> definitionsById,
        IReadOnlyDictionary<string, BattleSkillSnapshot> snapshotTemplatesById)
    {
        DefinitionsById = definitionsById;
        SnapshotTemplatesById = snapshotTemplatesById;
    }

    public IReadOnlyDictionary<string, BattleSkillDefinitionResource> DefinitionsById { get; }
    public IReadOnlyDictionary<string, BattleSkillSnapshot> SnapshotTemplatesById { get; }

    public static BattleSkillDefinitionCatalog Load(BattleSkillDefinitionIndex index)
    {
        if (index == null)
        {
            throw new ArgumentNullException(nameof(index));
        }

        Dictionary<string, BattleSkillSnapshot> templates = new(StringComparer.Ordinal);
        foreach ((string skillDefinitionId, string resourcePath) in index.ResourcePathsBySkillDefinitionId)
        {
            BattleSkillSnapshot template = LoadTextSnapshotTemplate(skillDefinitionId, resourcePath);

            string authoredId = template.SkillDefinitionId?.Trim() ?? "";
            if (!string.Equals(authoredId, skillDefinitionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"battle_skill_definition_id_mismatch id={skillDefinitionId} authored={authoredId} path={resourcePath}");
            }

            templates.Add(skillDefinitionId, template);
        }

        return new BattleSkillDefinitionCatalog(
            new Dictionary<string, BattleSkillDefinitionResource>(StringComparer.Ordinal),
            templates);
    }

    private static BattleSkillSnapshot LoadTextSnapshotTemplate(string skillDefinitionId, string resourcePath)
    {
        string source = ProjectConfigFileReader.ReadAllText(resourcePath);
        ParsedTresResource parsed = ParseTres(source);
        BattleSkillTargetingSnapshot targeting = BuildTargetingSnapshot(parsed, parsed.MainProperties);
        BattleSkillTimingSnapshot timing = BuildTimingSnapshot(parsed, parsed.MainProperties);
        BattleSkillInterruptPolicySnapshot interruptPolicy = BuildInterruptPolicySnapshot(parsed, parsed.MainProperties);
        BattleSkillPresentationSnapshot presentation = BuildPresentationSnapshot(parsed, parsed.MainProperties);
        BattleSkillSnapshot snapshot = new()
        {
            SkillDefinitionId = ReadString(parsed.MainProperties, "SkillDefinitionId", skillDefinitionId),
            DisplayName = ReadString(parsed.MainProperties, "DisplayName", ""),
            IconText = ReadString(parsed.MainProperties, "IconText", ""),
            IconPath = ReadString(parsed.MainProperties, "IconPath", ""),
            Tags = ParseStringArray(ReadRaw(parsed.MainProperties, "Tags")).ToList(),
            CommandChannel = (BattleSkillCommandChannel)ReadInt(parsed.MainProperties, "CommandChannel", 0),
            SkillType = (BattleSkillType)ReadInt(parsed.MainProperties, "SkillType", 0),
            Targeting = targeting,
            Timing = timing,
            InterruptPolicy = interruptPolicy,
            Costs = BuildCostSnapshots(parsed, parsed.MainProperties),
            Cooldown = BuildCooldownSnapshot(parsed, parsed.MainProperties),
            Presentation = presentation,
            TargetingMode = MapLegacyTargeting(targeting.InputFlow, targeting.TargetKind),
            Range = targeting.Range,
            CastSeconds = timing.CastSeconds,
            ImpactDelaySeconds = timing.ImpactDelaySeconds,
            RecoverySeconds = timing.RecoverySeconds,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = interruptPolicy.CanInterruptBasicAttackWindup,
            CanCancelBasicAttackRecovery = interruptPolicy.CanCancelBasicAttackRecovery,
            ReleasesWithoutOccupyingCaster = interruptPolicy.ReleasesWithoutOccupyingCaster
        };
        snapshot.Effects.AddRange(BuildEffectSnapshots(parsed, parsed.MainProperties, presentation.ProfileId));
        return snapshot;
    }

    private static BattleSkillTargetingSnapshot BuildTargetingSnapshot(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, mainProperties, "Targeting");
        return new BattleSkillTargetingSnapshot
        {
            InputFlow = (BattleSkillInputFlow)ReadInt(properties, "InputFlow", (int)BattleSkillInputFlow.SelectActor),
            TargetKind = (BattleSkillTargetKind)ReadInt(properties, "TargetKind", (int)BattleSkillTargetKind.Actor),
            Range = ReadInt(properties, "Range", 0),
            RangeMetric = (BattleSkillRangeMetric)ReadInt(properties, "RangeMetric", (int)BattleSkillRangeMetric.Manhattan),
            AreaShape = (BattleSkillAreaShape)ReadInt(properties, "AreaShape", (int)BattleSkillAreaShape.SingleActor),
            AreaRadius = ReadInt(properties, "AreaRadius", 0),
            DirectionMode = (BattleSkillDirectionMode)ReadInt(properties, "DirectionMode", (int)BattleSkillDirectionMode.None),
            RequiresSelectedMark = ReadBool(properties, "RequiresSelectedMark", false),
            RequiredMarkKind = (BattleSkillMarkKind)ReadInt(properties, "RequiredMarkKind", (int)BattleSkillMarkKind.None),
            LandingRadius = ReadInt(properties, "LandingRadius", 0),
            PreviewProfileId = ReadString(properties, "PreviewProfileId", "")
        };
    }

    private static BattleSkillTimingSnapshot BuildTimingSnapshot(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, mainProperties, "Timing");
        return new BattleSkillTimingSnapshot
        {
            CastSeconds = ReadDouble(properties, "CastSeconds", 0),
            ImpactDelaySeconds = ReadDouble(properties, "ImpactDelaySeconds", 0),
            RecoverySeconds = ReadDouble(properties, "RecoverySeconds", 0)
        };
    }

    private static BattleSkillInterruptPolicySnapshot BuildInterruptPolicySnapshot(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, mainProperties, "InterruptPolicy");
        return new BattleSkillInterruptPolicySnapshot
        {
            CanInterruptBasicAttackWindup = ReadBool(properties, "CanInterruptBasicAttackWindup", false),
            CanCancelBasicAttackRecovery = ReadBool(properties, "CanCancelBasicAttackRecovery", false),
            ReleasesWithoutOccupyingCaster = ReadBool(properties, "ReleasesWithoutOccupyingCaster", false),
            CanInterruptActiveChannel = ReadBool(properties, "CanInterruptActiveChannel", false)
        };
    }

    private static BattleSkillPresentationSnapshot BuildPresentationSnapshot(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, mainProperties, "Presentation");
        return new BattleSkillPresentationSnapshot
        {
            ProfileId = ReadString(properties, "ProfileId", ""),
            CastFxProfileId = ReadString(properties, "CastFxProfileId", ""),
            ImpactFxProfileId = ReadString(properties, "ImpactFxProfileId", ""),
            MarkFxProfileId = ReadString(properties, "MarkFxProfileId", ""),
            AreaFxProfileId = ReadString(properties, "AreaFxProfileId", ""),
            SuppressActorCastFx = ReadBool(properties, "SuppressActorCastFx", false),
            HoldCastAnimationDuringAction = ReadBool(properties, "HoldCastAnimationDuringAction", false)
        };
    }

    private static List<BattleSkillCostSnapshot> BuildCostSnapshots(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        List<BattleSkillCostSnapshot> costs = new();
        foreach (string id in ParseSubResourceIds(ReadRaw(mainProperties, "CostRules")))
        {
            IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, id);
            string scriptName = GetSubResourceScriptName(parsed, id);
            costs.Add(scriptName switch
            {
                "NoCostSkillCostRuleResource.cs" => new NoCostSkillCostSnapshot(),
                "ManaCostSkillCostRuleResource.cs" => new ManaCostSkillCostSnapshot
                {
                    PoolId = ReadString(properties, "PoolId", ""),
                    Amount = ReadInt(properties, "Amount", 0),
                    PayTiming = (BattleSkillCostPayTiming)ReadInt(properties, "PayTiming", 0),
                    RefundPolicy = (BattleSkillRefundPolicy)ReadInt(properties, "RefundPolicy", 0)
                },
                "LimitedUseSkillCostRuleResource.cs" => new LimitedUseSkillCostSnapshot
                {
                    MaxUses = ReadInt(properties, "MaxUses", 0),
                    ConsumeTiming = (BattleSkillCostPayTiming)ReadInt(properties, "ConsumeTiming", 0),
                    RefundPolicy = (BattleSkillRefundPolicy)ReadInt(properties, "RefundPolicy", 0)
                },
                _ => throw new InvalidOperationException($"battle_skill_cost_resource_unsupported type={scriptName}")
            });
        }

        if (costs.Count == 0)
        {
            costs.Add(new NoCostSkillCostSnapshot());
        }

        return costs;
    }

    private static BattleSkillCooldownSnapshot BuildCooldownSnapshot(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties)
    {
        string id = ParseSubResourceIds(ReadRaw(mainProperties, "CooldownRules")).FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(id))
        {
            return new NoCooldownSkillCooldownSnapshot();
        }

        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, id);
        string scriptName = GetSubResourceScriptName(parsed, id);
        return scriptName switch
        {
            "NoCooldownSkillCooldownRuleResource.cs" => new NoCooldownSkillCooldownSnapshot(),
            "PerGrantCooldownRuleResource.cs" => new PerGrantCooldownSkillCooldownSnapshot
            {
                DurationSeconds = ReadDouble(properties, "DurationSeconds", 0),
                StartsOn = (BattleSkillCooldownStart)ReadInt(properties, "StartsOn", 0),
                SharedCooldownGroupId = ReadString(properties, "SharedCooldownGroupId", "")
            },
            "ChargeCooldownRuleResource.cs" => new ChargeCooldownSkillCooldownSnapshot
            {
                MaxCharges = ReadInt(properties, "MaxCharges", 0),
                RechargeSeconds = ReadDouble(properties, "RechargeSeconds", 0),
                StartsFull = ReadBool(properties, "StartsFull", false)
            },
            _ => throw new InvalidOperationException($"battle_skill_cooldown_resource_unsupported type={scriptName}")
        };
    }

    private static IReadOnlyList<BattleSkillEffectSnapshot> BuildEffectSnapshots(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties,
        string presentationProfileId)
    {
        List<BattleSkillEffectSnapshot> effects = new();
        foreach (string id in ParseSubResourceIds(ReadRaw(mainProperties, "Effects")))
        {
            IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, id);
            string scriptName = GetSubResourceScriptName(parsed, id);
            BattleSkillEffectSnapshot effect = scriptName switch
            {
                "DamageSkillEffectResource.cs" => new DamageSkillEffectSnapshot
                {
                    BaseDamage = ReadInt(properties, "BaseDamage", 0),
                    DamageType = (BattleSkillDamageType)ReadInt(properties, "DamageType", 0),
                    CanHitActors = ReadBool(properties, "CanHitActors", true),
                    CanHitWorldObjects = ReadBool(properties, "CanHitWorldObjects", false)
                },
                "CreateMarkSkillEffectResource.cs" => new CreateMarkSkillEffectSnapshot
                {
                    MarkKind = (BattleSkillMarkKind)ReadInt(properties, "MarkKind", 0),
                    LifetimeSeconds = ReadDouble(properties, "LifetimeSeconds", 0),
                    AttachToActorWhenTargeted = ReadBool(properties, "AttachToActorWhenTargeted", false),
                    ReplaceExistingOwnedMark = ReadBool(properties, "ReplaceExistingOwnedMark", false),
                    EffectInstancePolicy = BattleSkillEffectInstancePolicy.RuntimeInstance
                },
                "TeleportToMarkSkillEffectResource.cs" => new TeleportToMarkSkillEffectSnapshot
                {
                    RequiredMarkKind = (BattleSkillMarkKind)ReadInt(properties, "RequiredMarkKind", 0),
                    LandingRadius = ReadInt(properties, "LandingRadius", 0),
                    ConsumesMark = ReadBool(properties, "ConsumesMark", false)
                },
                "ChanneledAreaDamageSkillEffectResource.cs" => new ChanneledAreaDamageSkillEffectSnapshot
                {
                    BaseDamage = ReadInt(properties, "BaseDamage", 0),
                    DamageType = (BattleSkillDamageType)ReadInt(properties, "DamageType", 0),
                    DurationSeconds = ReadDouble(properties, "DurationSeconds", 0),
                    TickIntervalSeconds = ReadDouble(properties, "TickIntervalSeconds", 0),
                    AreaShape = (BattleSkillAreaShape)ReadInt(properties, "AreaShape", 0),
                    Radius = ReadInt(properties, "Radius", 0),
                    FollowsCaster = ReadBool(properties, "FollowsCaster", false),
                    UsesTargetOffset = ReadBool(properties, "UsesTargetOffset", false),
                    EffectInstancePolicy = BattleSkillEffectInstancePolicy.RuntimeInstance
                },
                _ => throw new InvalidOperationException($"battle_skill_effect_resource_unsupported type={scriptName}")
            };
            effect.PresentationProfileId = presentationProfileId ?? "";
            effects.Add(effect);
        }

        return effects;
    }

    private static IReadOnlyDictionary<string, string> GetSubResourceProperties(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> mainProperties,
        string propertyName)
    {
        string id = ParseSingleSubResourceId(ReadRaw(mainProperties, propertyName));
        return string.IsNullOrWhiteSpace(id)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : GetSubResourceProperties(parsed, id);
    }

    private static IReadOnlyDictionary<string, string> GetSubResourceProperties(ParsedTresResource parsed, string id)
    {
        return parsed.SubResources.TryGetValue(id, out Dictionary<string, string> properties)
            ? properties
            : throw new InvalidOperationException($"battle_skill_subresource_missing id={id}");
    }

    private static string GetSubResourceScriptName(ParsedTresResource parsed, string id)
    {
        IReadOnlyDictionary<string, string> properties = GetSubResourceProperties(parsed, id);
        string scriptId = ParseExtResourceId(ReadRaw(properties, "script"));
        parsed.ExternalScriptPaths.TryGetValue(scriptId, out string scriptPath);
        return (scriptPath ?? "").Split('/').LastOrDefault() ?? "";
    }

    private static BattleSkillTargetingMode MapLegacyTargeting(
        BattleSkillInputFlow inputFlow,
        BattleSkillTargetKind targetKind)
    {
        if (inputFlow == BattleSkillInputFlow.SelectActorOrCell ||
            targetKind == BattleSkillTargetKind.ActorOrCell)
        {
            return BattleSkillTargetingMode.TargetedActorOrCell;
        }

        if (inputFlow is BattleSkillInputFlow.SelectCell or BattleSkillInputFlow.SelectMarkThenLandingCell or BattleSkillInputFlow.SelectDirectionArea ||
            targetKind is BattleSkillTargetKind.Cell or BattleSkillTargetKind.Direction or BattleSkillTargetKind.Mark)
        {
            return BattleSkillTargetingMode.TargetedCell;
        }

        return targetKind == BattleSkillTargetKind.Actor
            ? BattleSkillTargetingMode.TargetedActor
            : BattleSkillTargetingMode.None;
    }

    private static BattleSkillDefinitionResource LoadTextResource(string skillDefinitionId, string resourcePath)
    {
        string source = ProjectConfigFileReader.ReadAllText(resourcePath);
        ParsedTresResource parsed = ParseTres(source);
        BattleSkillDefinitionResource definition = CreateResource<BattleSkillDefinitionResource>();
        definition.SkillDefinitionId = ReadString(parsed.MainProperties, "SkillDefinitionId", skillDefinitionId);
        definition.DisplayName = ReadString(parsed.MainProperties, "DisplayName", "");
        definition.IconText = ReadString(parsed.MainProperties, "IconText", "");
        definition.IconPath = ReadString(parsed.MainProperties, "IconPath", "");
        definition.Tags = ToStringArray(ParseStringArray(ReadRaw(parsed.MainProperties, "Tags")));
        definition.CommandChannel = (BattleSkillCommandChannelDefinition)ReadInt(parsed.MainProperties, "CommandChannel", 0);
        definition.SkillType = (BattleSkillTypeDefinition)ReadInt(parsed.MainProperties, "SkillType", 0);
        definition.Timing = ReadSubResource(parsed, parsed.MainProperties, "Timing") as BattleSkillTimingResource;
        definition.InterruptPolicy = ReadSubResource(parsed, parsed.MainProperties, "InterruptPolicy") as BattleSkillInterruptPolicyResource;
        definition.Targeting = ReadSubResource(parsed, parsed.MainProperties, "Targeting") as BattleSkillTargetingProfileResource;
        definition.Presentation = ReadSubResource(parsed, parsed.MainProperties, "Presentation") as BattleSkillPresentationProfileResource;
        definition.CostRules = ToCostRuleArray(ReadSubResourceArray(parsed, parsed.MainProperties, "CostRules").OfType<BattleSkillCostRuleResource>());
        definition.CooldownRules = ToCooldownRuleArray(ReadSubResourceArray(parsed, parsed.MainProperties, "CooldownRules").OfType<BattleSkillCooldownRuleResource>());
        definition.Effects = ToEffectArray(ReadSubResourceArray(parsed, parsed.MainProperties, "Effects").OfType<BattleSkillEffectResource>());
        return definition;
    }

    private static ParsedTresResource ParseTres(string source)
    {
        ParsedTresResource parsed = new();
        Dictionary<string, string> currentProperties = null;
        foreach (string rawLine in (source ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("[ext_resource", StringComparison.Ordinal))
            {
                string id = MatchQuotedAttribute(line, "id");
                string path = MatchQuotedAttribute(line, "path");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    parsed.ExternalScriptPaths[id] = path;
                }

                currentProperties = null;
                continue;
            }

            if (line.StartsWith("[sub_resource", StringComparison.Ordinal))
            {
                string id = MatchQuotedAttribute(line, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException("battle_skill_resource_subresource_id_missing");
                }

                currentProperties = new Dictionary<string, string>(StringComparer.Ordinal);
                parsed.SubResources[id] = currentProperties;
                continue;
            }

            if (line.StartsWith("[resource]", StringComparison.Ordinal))
            {
                currentProperties = parsed.MainProperties;
                continue;
            }

            int separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator <= 0 || currentProperties == null)
            {
                continue;
            }

            currentProperties[line[..separator]] = line[(separator + 3)..];
        }

        return parsed;
    }

    private static object ReadSubResource(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        string id = ParseSingleSubResourceId(ReadRaw(properties, propertyName));
        return string.IsNullOrWhiteSpace(id) ? null : BuildSubResource(parsed, id);
    }

    private static IReadOnlyList<object> ReadSubResourceArray(
        ParsedTresResource parsed,
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        return ParseSubResourceIds(ReadRaw(properties, propertyName))
            .Select(id => BuildSubResource(parsed, id))
            .Where(item => item != null)
            .ToArray();
    }

    private static object BuildSubResource(ParsedTresResource parsed, string subResourceId)
    {
        if (!parsed.SubResources.TryGetValue(subResourceId, out Dictionary<string, string> properties))
        {
            throw new InvalidOperationException($"battle_skill_subresource_missing id={subResourceId}");
        }

        string scriptId = ParseExtResourceId(ReadRaw(properties, "script"));
        parsed.ExternalScriptPaths.TryGetValue(scriptId, out string scriptPath);
        string scriptName = (scriptPath ?? "").Split('/').LastOrDefault() ?? "";
        return scriptName switch
        {
            "BattleSkillTimingResource.cs" => BuildTiming(properties),
            "BattleSkillInterruptPolicyResource.cs" => BuildInterruptPolicy(properties),
            "BattleSkillTargetingProfileResource.cs" => BuildTargeting(properties),
            "BattleSkillPresentationProfileResource.cs" => BuildPresentation(properties),
            "DamageSkillEffectResource.cs" => BuildDamageEffect(properties),
            "CreateMarkSkillEffectResource.cs" => BuildCreateMarkEffect(properties),
            "TeleportToMarkSkillEffectResource.cs" => BuildTeleportEffect(properties),
            "ChanneledAreaDamageSkillEffectResource.cs" => BuildChannelEffect(properties),
            "NoCostSkillCostRuleResource.cs" => CreateResource<NoCostSkillCostRuleResource>(),
            "ManaCostSkillCostRuleResource.cs" => BuildManaCost(properties),
            "LimitedUseSkillCostRuleResource.cs" => BuildLimitedUseCost(properties),
            "NoCooldownSkillCooldownRuleResource.cs" => CreateResource<NoCooldownSkillCooldownRuleResource>(),
            "PerGrantCooldownRuleResource.cs" => BuildPerGrantCooldown(properties),
            "ChargeCooldownRuleResource.cs" => BuildChargeCooldown(properties),
            _ => throw new InvalidOperationException($"battle_skill_subresource_script_unsupported id={subResourceId} script={scriptName}")
        };
    }

    private static BattleSkillTimingResource BuildTiming(IReadOnlyDictionary<string, string> properties)
    {
        BattleSkillTimingResource resource = CreateResource<BattleSkillTimingResource>();
        resource.CastSeconds = ReadDouble(properties, "CastSeconds", 0);
        resource.ImpactDelaySeconds = ReadDouble(properties, "ImpactDelaySeconds", 0);
        resource.RecoverySeconds = ReadDouble(properties, "RecoverySeconds", 0);
        return resource;
    }

    private static BattleSkillInterruptPolicyResource BuildInterruptPolicy(IReadOnlyDictionary<string, string> properties)
    {
        BattleSkillInterruptPolicyResource resource = CreateResource<BattleSkillInterruptPolicyResource>();
        resource.CanInterruptBasicAttackWindup = ReadBool(properties, "CanInterruptBasicAttackWindup", false);
        resource.CanCancelBasicAttackRecovery = ReadBool(properties, "CanCancelBasicAttackRecovery", false);
        resource.ReleasesWithoutOccupyingCaster = ReadBool(properties, "ReleasesWithoutOccupyingCaster", false);
        resource.CanInterruptActiveChannel = ReadBool(properties, "CanInterruptActiveChannel", false);
        return resource;
    }

    private static BattleSkillTargetingProfileResource BuildTargeting(IReadOnlyDictionary<string, string> properties)
    {
        BattleSkillTargetingProfileResource resource = CreateResource<BattleSkillTargetingProfileResource>();
        resource.InputFlow = (BattleSkillInputFlowDefinition)ReadInt(properties, "InputFlow", (int)BattleSkillInputFlowDefinition.SelectActor);
        resource.TargetKind = (BattleSkillTargetKindDefinition)ReadInt(properties, "TargetKind", (int)BattleSkillTargetKindDefinition.Actor);
        resource.Range = ReadInt(properties, "Range", 0);
        resource.RangeMetric = (BattleSkillRangeMetricDefinition)ReadInt(properties, "RangeMetric", (int)BattleSkillRangeMetricDefinition.Manhattan);
        resource.AreaShape = (BattleSkillAreaShapeDefinition)ReadInt(properties, "AreaShape", (int)BattleSkillAreaShapeDefinition.SingleActor);
        resource.AreaRadius = ReadInt(properties, "AreaRadius", 0);
        resource.DirectionMode = (BattleSkillDirectionModeDefinition)ReadInt(properties, "DirectionMode", (int)BattleSkillDirectionModeDefinition.None);
        resource.RequiresSelectedMark = ReadBool(properties, "RequiresSelectedMark", false);
        resource.RequiredMarkKind = (BattleSkillMarkKindDefinition)ReadInt(properties, "RequiredMarkKind", (int)BattleSkillMarkKindDefinition.None);
        resource.LandingRadius = ReadInt(properties, "LandingRadius", 0);
        resource.PreviewProfileId = ReadString(properties, "PreviewProfileId", "");
        return resource;
    }

    private static BattleSkillPresentationProfileResource BuildPresentation(IReadOnlyDictionary<string, string> properties)
    {
        BattleSkillPresentationProfileResource resource = CreateResource<BattleSkillPresentationProfileResource>();
        resource.ProfileId = ReadString(properties, "ProfileId", "");
        resource.CastFxProfileId = ReadString(properties, "CastFxProfileId", "");
        resource.ImpactFxProfileId = ReadString(properties, "ImpactFxProfileId", "");
        resource.MarkFxProfileId = ReadString(properties, "MarkFxProfileId", "");
        resource.AreaFxProfileId = ReadString(properties, "AreaFxProfileId", "");
        resource.SuppressActorCastFx = ReadBool(properties, "SuppressActorCastFx", false);
        resource.HoldCastAnimationDuringAction = ReadBool(properties, "HoldCastAnimationDuringAction", false);
        return resource;
    }

    private static DamageSkillEffectResource BuildDamageEffect(IReadOnlyDictionary<string, string> properties)
    {
        DamageSkillEffectResource resource = CreateResource<DamageSkillEffectResource>();
        resource.BaseDamage = ReadInt(properties, "BaseDamage", 0);
        resource.DamageType = (BattleSkillDamageTypeDefinition)ReadInt(properties, "DamageType", (int)BattleSkillDamageTypeDefinition.Physical);
        resource.CanHitActors = ReadBool(properties, "CanHitActors", true);
        resource.CanHitWorldObjects = ReadBool(properties, "CanHitWorldObjects", false);
        return resource;
    }

    private static CreateMarkSkillEffectResource BuildCreateMarkEffect(IReadOnlyDictionary<string, string> properties)
    {
        CreateMarkSkillEffectResource resource = CreateResource<CreateMarkSkillEffectResource>();
        resource.MarkKind = (BattleSkillMarkKindDefinition)ReadInt(properties, "MarkKind", (int)BattleSkillMarkKindDefinition.None);
        resource.LifetimeSeconds = ReadDouble(properties, "LifetimeSeconds", 0);
        resource.AttachToActorWhenTargeted = ReadBool(properties, "AttachToActorWhenTargeted", false);
        resource.ReplaceExistingOwnedMark = ReadBool(properties, "ReplaceExistingOwnedMark", false);
        return resource;
    }

    private static TeleportToMarkSkillEffectResource BuildTeleportEffect(IReadOnlyDictionary<string, string> properties)
    {
        TeleportToMarkSkillEffectResource resource = CreateResource<TeleportToMarkSkillEffectResource>();
        resource.RequiredMarkKind = (BattleSkillMarkKindDefinition)ReadInt(properties, "RequiredMarkKind", (int)BattleSkillMarkKindDefinition.None);
        resource.LandingRadius = ReadInt(properties, "LandingRadius", 0);
        resource.ConsumesMark = ReadBool(properties, "ConsumesMark", false);
        return resource;
    }

    private static ChanneledAreaDamageSkillEffectResource BuildChannelEffect(IReadOnlyDictionary<string, string> properties)
    {
        ChanneledAreaDamageSkillEffectResource resource = CreateResource<ChanneledAreaDamageSkillEffectResource>();
        resource.BaseDamage = ReadInt(properties, "BaseDamage", 0);
        resource.DamageType = (BattleSkillDamageTypeDefinition)ReadInt(properties, "DamageType", (int)BattleSkillDamageTypeDefinition.Physical);
        resource.DurationSeconds = ReadDouble(properties, "DurationSeconds", 0);
        resource.TickIntervalSeconds = ReadDouble(properties, "TickIntervalSeconds", 0);
        resource.AreaShape = (BattleSkillAreaShapeDefinition)ReadInt(properties, "AreaShape", (int)BattleSkillAreaShapeDefinition.SingleActor);
        resource.Radius = ReadInt(properties, "Radius", 0);
        resource.FollowsCaster = ReadBool(properties, "FollowsCaster", false);
        resource.UsesTargetOffset = ReadBool(properties, "UsesTargetOffset", false);
        return resource;
    }

    private static ManaCostSkillCostRuleResource BuildManaCost(IReadOnlyDictionary<string, string> properties)
    {
        ManaCostSkillCostRuleResource resource = CreateResource<ManaCostSkillCostRuleResource>();
        resource.PoolId = ReadString(properties, "PoolId", "");
        resource.Amount = ReadInt(properties, "Amount", 0);
        resource.PayTiming = (BattleSkillCostPayTimingDefinition)ReadInt(properties, "PayTiming", 0);
        resource.RefundPolicy = (BattleSkillRefundPolicyDefinition)ReadInt(properties, "RefundPolicy", 0);
        return resource;
    }

    private static LimitedUseSkillCostRuleResource BuildLimitedUseCost(IReadOnlyDictionary<string, string> properties)
    {
        LimitedUseSkillCostRuleResource resource = CreateResource<LimitedUseSkillCostRuleResource>();
        resource.MaxUses = ReadInt(properties, "MaxUses", 0);
        resource.ConsumeTiming = (BattleSkillCostPayTimingDefinition)ReadInt(properties, "ConsumeTiming", 0);
        resource.RefundPolicy = (BattleSkillRefundPolicyDefinition)ReadInt(properties, "RefundPolicy", 0);
        return resource;
    }

    private static PerGrantCooldownRuleResource BuildPerGrantCooldown(IReadOnlyDictionary<string, string> properties)
    {
        PerGrantCooldownRuleResource resource = CreateResource<PerGrantCooldownRuleResource>();
        resource.DurationSeconds = ReadDouble(properties, "DurationSeconds", 0);
        resource.StartsOn = (BattleSkillCooldownStartDefinition)ReadInt(properties, "StartsOn", 0);
        resource.SharedCooldownGroupId = ReadString(properties, "SharedCooldownGroupId", "");
        return resource;
    }

    private static ChargeCooldownRuleResource BuildChargeCooldown(IReadOnlyDictionary<string, string> properties)
    {
        ChargeCooldownRuleResource resource = CreateResource<ChargeCooldownRuleResource>();
        resource.MaxCharges = ReadInt(properties, "MaxCharges", 0);
        resource.RechargeSeconds = ReadDouble(properties, "RechargeSeconds", 0);
        resource.StartsFull = ReadBool(properties, "StartsFull", false);
        return resource;
    }

    private static T CreateResource<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static Godot.Collections.Array<string> ToStringArray(IEnumerable<string> items)
    {
        Godot.Collections.Array<string> array = new();
        foreach (string item in items ?? Enumerable.Empty<string>())
        {
            array.Add(item);
        }

        return array;
    }

    private static Godot.Collections.Array<BattleSkillCostRuleResource> ToCostRuleArray(IEnumerable<BattleSkillCostRuleResource> items)
    {
        Godot.Collections.Array<BattleSkillCostRuleResource> array = new();
        foreach (BattleSkillCostRuleResource item in items ?? Enumerable.Empty<BattleSkillCostRuleResource>())
        {
            array.Add(item);
        }

        return array;
    }

    private static Godot.Collections.Array<BattleSkillCooldownRuleResource> ToCooldownRuleArray(IEnumerable<BattleSkillCooldownRuleResource> items)
    {
        Godot.Collections.Array<BattleSkillCooldownRuleResource> array = new();
        foreach (BattleSkillCooldownRuleResource item in items ?? Enumerable.Empty<BattleSkillCooldownRuleResource>())
        {
            array.Add(item);
        }

        return array;
    }

    private static Godot.Collections.Array<BattleSkillEffectResource> ToEffectArray(IEnumerable<BattleSkillEffectResource> items)
    {
        Godot.Collections.Array<BattleSkillEffectResource> array = new();
        foreach (BattleSkillEffectResource item in items ?? Enumerable.Empty<BattleSkillEffectResource>())
        {
            array.Add(item);
        }

        return array;
    }

    private static string MatchQuotedAttribute(string line, string attribute)
    {
        Match match = Regex.Match(line ?? "", $@"\b{Regex.Escape(attribute)}=""([^""]*)""");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string ReadRaw(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties != null && properties.TryGetValue(key, out string value)
            ? value
            : "";
    }

    private static string ReadString(IReadOnlyDictionary<string, string> properties, string key, string fallback)
    {
        string raw = ReadRaw(properties, key).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback ?? "";
        }

        int first = raw.IndexOf('"');
        int last = raw.LastIndexOf('"');
        if (first >= 0 && last > first)
        {
            return raw.Substring(first + 1, last - first - 1);
        }

        return first >= 0 ? raw[(first + 1)..] : raw;
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> properties, string key, int fallback)
    {
        return int.TryParse(ReadRaw(properties, key), out int value) ? value : fallback;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> properties, string key, double fallback)
    {
        return double.TryParse(ReadRaw(properties, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        string raw = ReadRaw(properties, key);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParseStringArray(string raw)
    {
        foreach (Match match in Regex.Matches(raw ?? "", @"""([^""]*)"""))
        {
            yield return match.Groups[1].Value;
        }
    }

    private static string ParseSingleSubResourceId(string raw)
    {
        return ParseSubResourceIds(raw).FirstOrDefault() ?? "";
    }

    private static IEnumerable<string> ParseSubResourceIds(string raw)
    {
        foreach (Match match in Regex.Matches(raw ?? "", @"SubResource\(""([^""]+)""\)"))
        {
            yield return match.Groups[1].Value;
        }
    }

    private static string ParseExtResourceId(string raw)
    {
        Match match = Regex.Match(raw ?? "", @"ExtResource\(""([^""]+)""\)");
        return match.Success ? match.Groups[1].Value : "";
    }

    private sealed class ParsedTresResource
    {
        public Dictionary<string, string> ExternalScriptPaths { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, string>> SubResources { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> MainProperties { get; } = new(StringComparer.Ordinal);
    }

    public static BattleSkillDefinitionCatalog FromDefinitions(IEnumerable<BattleSkillDefinitionResource> definitions)
    {
        Dictionary<string, BattleSkillDefinitionResource> result = new(StringComparer.Ordinal);
        foreach (BattleSkillDefinitionResource definition in definitions ?? Enumerable.Empty<BattleSkillDefinitionResource>())
        {
            string skillDefinitionId = definition?.SkillDefinitionId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(skillDefinitionId))
            {
                throw new InvalidOperationException("battle_skill_definition_id_missing");
            }

            if (!result.TryAdd(skillDefinitionId, definition))
            {
                throw new InvalidOperationException($"battle_skill_definition_duplicate id={skillDefinitionId}");
            }
        }

        return new BattleSkillDefinitionCatalog(result);
    }
}
