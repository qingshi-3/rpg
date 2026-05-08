using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot : Control
{
    private const float OuterMargin = 26.0f;
    private const float TopBarHeight = 70.0f;
    private const float DetailWidth = 520.0f;
    private const float SiteIconRadius = 24.0f;
    private const float SiteVisualHitPadding = 10.0f;
    private const float SiteVisualLabelGap = 5.0f;
    private const float OpportunityMarkerRadius = 18.0f;
    private const float SiteApproachVisualOffset = 8.0f;
    private const float SiteApproachEdgeNudge = 2.0f;
    private const float SiteNavigationPointSnapDistance = 96.0f;
    private const int SiteNavigationPointSearchCellRadius = 8;
    private const double DefaultWorldTickIntervalSeconds = 8.0;
    private const string StrategicNavigationTileLayerName = "StrategicNavigationTileLayer";
    private static readonly Vector2 SiteButtonSize = new(132.0f, 106.0f);
    private static readonly Vector2 SiteLabelFallbackSize = new(SiteButtonSize.X + 28.0f, 44.0f);
    private static readonly double[] WorldClockSpeedMultipliers = { 1.0, 2.0, 4.0 };
    private static readonly Vector2I[] SiteVisualScanDirections =
    {
        new(-1, 0),
        new(1, 0),
        new(0, -1),
        new(0, 1)
    };

    [Export]
    public string SiteScenePath { get; set; } = "res://scenes/world/sites/WorldSiteRoot.tscn";

    [Export]
    public NodePath WorldMapRootPath { get; set; } = new("WorldMapRoot");

    [Export]
    public NodePath WorldCameraPath { get; set; } = new("WorldCamera");

    [Export]
    public NodePath SiteAnchorRootPath { get; set; } = new("WorldMapRoot/MapAnchors/Sites");

    [Export]
    public NodePath SiteVisualLayerPath { get; set; } = new("WorldMapRoot/SiteVisualLayer");

    [Export]
    public NodePath ArmySpawnPointRootPath { get; set; } = new("WorldMapRoot/MapAnchors/ArmySpawnPoints");

    [Export]
    public NodePath EncounterZoneRootPath { get; set; } = new("WorldMapRoot/MapAnchors/EncounterZones");

    [Export]
    public bool AutoWorldClockEnabled { get; set; } = true;

    [Export]
    public double WorldTickIntervalSeconds { get; set; } = DefaultWorldTickIntervalSeconds;

    private readonly WorldActionResolver _actionResolver = new();
    private readonly WorldBattleResultApplier _battleResultApplier = new();
    private readonly WorldBattleRequestBuilder _battleRequestBuilder = new();
    private readonly WorldArmyMovementService _armyMovementService = new();
    private readonly WorldExpeditionService _expeditionService = new();
    private readonly WorldOpportunityService _opportunityService = new();
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly StrategicWorldSaveService _saveService = new();
    private readonly WorldTickService _worldTickService = new();
    private readonly WorldBattleProgressionService _worldBattleProgressionService = new();

    private readonly Dictionary<string, Button> _siteButtons = new();
    private readonly Dictionary<string, Label> _siteLabels = new();
    private readonly Dictionary<string, SiteVisualFootprint> _siteVisualFootprints = new();
    private readonly HashSet<string> _reportedThreatNavigationFailures = new();
    private readonly HashSet<string> _reportedSiteVisualFootprintFailures = new();
    private readonly HashSet<string> _reportedSiteNavigationPointResolutions = new();
    private readonly HashSet<string> _recoveredStalledThreatArmies = new();
    private Node2D _worldMapRoot;
    private MapCameraController _worldCamera;
    private Node2D _siteAnchorRoot;
    private Node2D _armySpawnPointRoot;
    private TileMapLayer _siteVisualLayer;
    private StrategicNavigationContext _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable("strategic_navigation_not_configured");
    private Label _resourceLabel;
    private Label _worldClockLabel;
    private Label _noticeLabel;
    private Button _worldClockToggleButton;
    private Button _worldClockSpeedButton;
    private Label _siteTitleLabel;
    private Label _siteBodyLabel;
    private VBoxContainer _facilityList;
    private VBoxContainer _garrisonList;
    private VBoxContainer _threatList;
    private VBoxContainer _actionList;
    private VBoxContainer _siteDetailContent;
    private Label _facilityTitleLabel;
    private Label _garrisonTitleLabel;
    private Label _actionTitleLabel;
    private WorldOpportunityDetailPanel _opportunityDetailPanel;

    private string _selectedSiteId = StrategicWorldIds.SitePlayerCamp;
    private string _selectedThreatId = "";
    private string _selectedOpportunityId = "";
    private readonly HashSet<string> _selectedArmyIds = new();
    private bool _isExpeditionDrafting;
    private bool _isExpeditionTargeting;
    private string _expeditionSourceSiteId = "";
    private readonly Dictionary<string, int> _expeditionUnitCounts = new();
    private bool _isArmyBoxSelecting;
    private Vector2 _armySelectionStartScreen;
    private Vector2 _armySelectionCurrentScreen;
    private bool _worldClockPaused;
    private int _worldClockSpeedIndex = 2;
    private double _worldClockAccumulator;
    private BattleStartRequest _pendingBattleRequest;
    private AcceptDialog _battleAlertDialog;
    private AcceptDialog _preBattleDialog;
    private string _activeBattleGateDialog = "";
    private Vector2 _lastWorldMapRootPosition = new(float.NaN, float.NaN);
    private Vector2 _lastWorldMapRootScale = new(float.NaN, float.NaN);

    private sealed class SiteVisualFootprint
    {
        public SiteVisualFootprint(string siteId, HashSet<Vector2I> cells, Rect2 mapBounds)
        {
            SiteId = siteId;
            Cells = cells;
            MapBounds = mapBounds;
        }

        public string SiteId { get; }
        public HashSet<Vector2I> Cells { get; }
        public Rect2 MapBounds { get; }
    }

    private StrategicWorldDefinition Definition => StrategicWorldRuntime.Definition;
    private StrategicWorldState State => StrategicWorldRuntime.State;

    public override void _Ready()
    {
        GameLog.StartSession(nameof(StrategicWorldRoot));
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);

        StrategicWorldRuntime.EnsureInitialized();
        ResolveWorldMapNodes();
        ResolveWorldCamera();
        ConfigureStrategicNavigationContext();
        SyncDefinitionMapPositionsFromAnchors();
        RebuildSiteVisualFootprints();
        RecoverUnsupportedPlayerAssaultArmies();
        ConfigureWorldCamera();
        UpdateWorldCameraView(true);
        _worldClockPaused = HasAttackingThreat();
        BuildUi();
        ConsumeBattleResult();
        _worldClockPaused = HasAttackingThreat();
        EnsureWorldBattlesForAttackingThreats();
        _worldClockPaused = HasAttackingThreat();
        SelectSite(string.IsNullOrWhiteSpace(_selectedSiteId) ? Definition.StartingSiteId : _selectedSiteId);
        RefreshAll();
        TryEnterFirstDefenseRaidBattle();
        GameLog.Info(nameof(StrategicWorldRoot), "Strategic world root ready.");
    }

    public override void _Process(double delta)
    {
        UpdateWorldArmyMovement(delta);
        UpdateWorldClock(delta);
        UpdateWorldCameraView();
    }

    public override void _GuiInput(InputEvent @event)
    {
        HandleWorldArmyInput(@event);
    }

    public override void _Draw()
    {
        if (Definition == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        Rect2 mapBounds = GetMapBounds();
        if (!HasConfiguredWorldMapSurface())
        {
            DrawStrategicMapBackground(mapBounds);
        }

        DrawSiteIcons(queries);
        DrawLegacyThreatMarkers(queries);
        DrawWorldOpportunities(queries);
        DrawWorldArmies();
        DrawArmySelectionBox();
    }

    private void DrawStrategicMapBackground(Rect2 mapBounds)
    {
        DrawRect(mapBounds, new Color(0.18f, 0.25f, 0.19f, 1.0f), true);
        DrawRect(mapBounds, new Color(0.04f, 0.05f, 0.045f, 1.0f), false, 2.0f);

        // 地貌先作为战略地图底色表达，不替代后续手工 TileMap。
        DrawRect(new Rect2(mapBounds.Position + new Vector2(28.0f, 34.0f), new Vector2(390.0f, 180.0f)), new Color(0.22f, 0.33f, 0.21f, 0.92f), true);
        DrawRect(new Rect2(mapBounds.Position + new Vector2(510.0f, 60.0f), new Vector2(280.0f, 150.0f)), new Color(0.34f, 0.33f, 0.27f, 0.82f), true);
        DrawRect(new Rect2(mapBounds.Position + new Vector2(840.0f, 64.0f), new Vector2(310.0f, 190.0f)), new Color(0.22f, 0.21f, 0.23f, 0.86f), true);

        Vector2[] river =
        {
            mapBounds.Position + new Vector2(60.0f, mapBounds.Size.Y - 210.0f),
            mapBounds.Position + new Vector2(300.0f, mapBounds.Size.Y - 176.0f),
            mapBounds.Position + new Vector2(560.0f, mapBounds.Size.Y - 205.0f),
            mapBounds.Position + new Vector2(840.0f, mapBounds.Size.Y - 150.0f),
            mapBounds.Position + new Vector2(1120.0f, mapBounds.Size.Y - 178.0f)
        };
        for (int i = 0; i < river.Length - 1; i++)
        {
            DrawLine(river[i], river[i + 1], new Color(0.18f, 0.34f, 0.42f, 0.72f), 16.0f, true);
            DrawLine(river[i], river[i + 1], new Color(0.32f, 0.55f, 0.64f, 0.78f), 5.0f, true);
        }

        for (int i = 0; i < 6; i++)
        {
            Vector2 basePoint = mapBounds.Position + new Vector2(890.0f + i * 46.0f, 118.0f + (i % 2) * 22.0f);
            DrawPolygon(
                new[] { basePoint + new Vector2(0, 38), basePoint + new Vector2(24, 0), basePoint + new Vector2(48, 38) },
                new[] { new Color(0.28f, 0.27f, 0.29f, 0.94f) });
        }
    }

    private void DrawLegacyThreatMarkers(StrategicWorldDefinitionQueries queries)
    {
        foreach (EnemyThreatPlan threat in State.ThreatPlans.Values.Where(threat => threat.Stage != ThreatStage.Resolved))
        {
            if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                State.ArmyStates.ContainsKey(threat.WorldArmyId))
            {
                continue;
            }

            WorldSiteDefinition source = queries.GetSite(threat.SourceSiteId);
            WorldSiteDefinition target = queries.GetSite(threat.TargetSiteId);
            if (source == null || target == null)
            {
                continue;
            }

            Vector2 sourceCenter = GetSiteCenter(source);
            Vector2 targetCenter = GetSiteCenter(target);
            List<Vector2> navigationPoints = GetLegacyThreatNavigationPoints(threat, sourceCenter, targetCenter);
            if (navigationPoints.Count == 0)
            {
                continue;
            }

            if (threat.Stage == ThreatStage.Attacking)
            {
                DrawArc(targetCenter, SiteIconRadius + 13.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.23f, 0.15f, 0.92f), 4.0f, true);
                DrawThreatArmyMarker(targetCenter + new Vector2(34.0f, -28.0f), true);
                continue;
            }

            int initialCountdown = threat.InitialCountdownTicks > 0 ? threat.InitialCountdownTicks : 3;
            float progress = 1.0f - Mathf.Clamp(threat.CountdownTicks / (float)initialCountdown, 0.0f, 1.0f);
            Vector2 marker = SamplePolyline(navigationPoints, Mathf.Clamp(0.08f + progress * 0.84f, 0.08f, 0.92f));
            DrawThreatArmyMarker(marker, false);
        }
    }

    private void DrawThreatArmyMarker(Vector2 position, bool attacking)
    {
        Color fill = attacking
            ? new Color(1.0f, 0.12f, 0.08f, 1.0f)
            : new Color(0.88f, 0.18f, 0.12f, 1.0f);
        Color dark = new(0.12f, 0.035f, 0.03f, 1.0f);
        DrawRect(new Rect2(position - new Vector2(11.0f, 11.0f), new Vector2(22.0f, 22.0f)), dark, true);
        DrawRect(new Rect2(position - new Vector2(8.0f, 8.0f), new Vector2(16.0f, 16.0f)), fill, true);
        DrawLine(position + new Vector2(-2.0f, -16.0f), position + new Vector2(-2.0f, 12.0f), dark, 3.0f, true);
        DrawPolygon(
            new[] { position + new Vector2(0.0f, -16.0f), position + new Vector2(18.0f, -10.0f), position + new Vector2(0.0f, -4.0f) },
            new[] { attacking ? new Color(1.0f, 0.32f, 0.14f, 1.0f) : new Color(0.78f, 0.12f, 0.08f, 1.0f) });
    }

    private void DrawWorldArmies()
    {
        if (State?.ArmyStates == null)
        {
            return;
        }

        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.Status == WorldArmyStatus.Defeated ||
                army.Status == WorldArmyStatus.Garrisoned)
            {
                continue;
            }

            DrawWorldArmyMarker(army);
        }
    }

    private void DrawWorldOpportunities(StrategicWorldDefinitionQueries queries)
    {
        if (State?.OpportunityStates == null)
        {
            return;
        }

        foreach (WorldOpportunityState opportunity in State.OpportunityStates.Values)
        {
            if (opportunity.Status != WorldOpportunityStatus.Active)
            {
                continue;
            }

            DrawWorldOpportunityMarker(opportunity, queries.GetOpportunity(opportunity.DefinitionId));
        }
    }

    private void DrawWorldOpportunityMarker(WorldOpportunityState opportunity, WorldOpportunityDefinition definition)
    {
        Vector2 position = MapToScreen(opportunity.WorldPosition);
        bool selected = opportunity.OpportunityId == _selectedOpportunityId;
        int remainingTicks = Mathf.Max(0, opportunity.ExpiresTick - State.WorldTick);
        float pulse = 1.0f + Mathf.Clamp(remainingTicks, 0, 5) * 0.03f;
        Color fill = new(0.95f, 0.72f, 0.22f, 0.94f);
        Color border = new(0.14f, 0.08f, 0.02f, 0.98f);
        float radius = OpportunityMarkerRadius * pulse;

        if (selected)
        {
            DrawArc(position, radius + 8.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.95f, 0.55f, 0.98f), 3.0f, true);
        }

        DrawCircle(position, radius + 4.0f, border);
        DrawCircle(position, radius, fill);
        DrawPolygon(
            new[]
            {
                position + new Vector2(0.0f, -radius - 10.0f),
                position + new Vector2(10.0f, -radius + 4.0f),
                position + new Vector2(-10.0f, -radius + 4.0f)
            },
            new[] { new Color(1.0f, 0.9f, 0.32f, 1.0f) });
        if (definition != null)
        {
            DrawString(ThemeDB.FallbackFont, position + new Vector2(-42.0f, radius + 20.0f), definition.DisplayName, HorizontalAlignment.Center, 84.0f, 13, new Color(1.0f, 0.92f, 0.68f, 0.95f));
        }
    }

    private void DrawWorldArmyMarker(WorldArmyState army)
    {
        Vector2 position = MapToScreen(army.WorldPosition);
        bool playerOwned = army.OwnerFactionId == State.PlayerFactionId;
        Color fill = playerOwned
            ? new Color(0.35f, 0.72f, 0.92f, 1.0f)
            : new Color(0.88f, 0.18f, 0.12f, 1.0f);
        Color border = new(0.04f, 0.045f, 0.04f, 0.98f);
        float radius = Mathf.Clamp(army.Radius, 10.0f, 28.0f);

        if (army.Status == WorldArmyStatus.Attacking)
        {
            DrawArc(position, radius + 11.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.23f, 0.15f, 0.92f), 4.0f, true);
        }

        if (_selectedArmyIds.Contains(army.ArmyId))
        {
            DrawArc(position, radius + 8.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.9f, 0.38f, 0.98f), 3.0f, true);
        }

        DrawCircle(position, radius + 4.0f, border);
        DrawCircle(position, radius, fill);
        DrawLine(position + new Vector2(-3.0f, -radius - 11.0f), position + new Vector2(-3.0f, radius * 0.65f), border, 3.0f, true);
        DrawPolygon(
            new[] { position + new Vector2(0.0f, -radius - 12.0f), position + new Vector2(20.0f, -radius - 6.0f), position + new Vector2(0.0f, -radius) },
            new[] { playerOwned ? new Color(0.62f, 0.9f, 1.0f, 1.0f) : new Color(1.0f, 0.3f, 0.15f, 1.0f) });

        if (army.Status == WorldArmyStatus.Moving)
        {
            DrawCircle(MapToScreen(army.Destination), 5.0f, new Color(fill.R, fill.G, fill.B, 0.75f));
        }
    }

    private void DrawArmySelectionBox()
    {
        if (!_isArmyBoxSelecting)
        {
            return;
        }

        Rect2 rect = BuildScreenRect(_armySelectionStartScreen, _armySelectionCurrentScreen);
        DrawRect(rect, new Color(0.32f, 0.7f, 0.95f, 0.16f), true);
        DrawRect(rect, new Color(0.52f, 0.86f, 1.0f, 0.9f), false, 2.0f);
    }

    private void DrawSiteIcons(StrategicWorldDefinitionQueries queries)
    {
        foreach (WorldSiteDefinition definition in Definition.SiteDefinitions)
        {
            WorldSiteState state = State.SiteStates[definition.Id];
            Vector2 center = GetSiteCenter(definition);
            Color color = GetSiteColor(state);
            bool selected = definition.Id == _selectedSiteId;

            if (TryGetSiteVisualScreenBounds(definition.Id, out Rect2 visualBounds))
            {
                DrawSiteVisualOverlay(state, visualBounds, selected);
                continue;
            }

            DrawCircle(center, SiteIconRadius + (selected ? 10.0f : 6.0f), selected ? new Color(1.0f, 0.86f, 0.32f, 0.95f) : new Color(0.04f, 0.045f, 0.04f, 0.95f));
            DrawCircle(center, SiteIconRadius + 2.0f, new Color(0.08f, 0.08f, 0.075f, 1.0f));
            DrawSiteSymbol(definition.SiteKind, center, color);

            if (state.ControlState == SiteControlState.Damaged)
            {
                DrawLine(center + new Vector2(-18.0f, -20.0f), center + new Vector2(18.0f, 20.0f), new Color(0.95f, 0.72f, 0.2f, 1.0f), 4.0f, true);
            }
        }
    }

    private void DrawSiteVisualOverlay(WorldSiteState state, Rect2 visualBounds, bool selected)
    {
        Color stateColor = GetSiteColor(state);
        Rect2 outline = visualBounds.Grow(selected ? 8.0f : 3.0f);
        if (selected)
        {
            DrawRect(outline, new Color(1.0f, 0.86f, 0.32f, 0.12f), true);
            DrawRect(outline, new Color(1.0f, 0.86f, 0.32f, 0.98f), false, 3.0f);
        }
        else
        {
            DrawRect(outline, new Color(stateColor.R, stateColor.G, stateColor.B, 0.55f), false, 2.0f);
        }

        if (state.ControlState == SiteControlState.Damaged)
        {
            Vector2 start = visualBounds.Position + new Vector2(6.0f, 6.0f);
            Vector2 end = visualBounds.End - new Vector2(6.0f, 6.0f);
            DrawLine(start, end, new Color(0.95f, 0.72f, 0.2f, 1.0f), 4.0f, true);
        }
    }

    private void DrawSiteSymbol(WorldSiteKind kind, Vector2 center, Color color)
    {
        switch (kind)
        {
            case WorldSiteKind.Base:
                DrawRect(new Rect2(center - new Vector2(20.0f, 8.0f), new Vector2(40.0f, 26.0f)), color, true);
                DrawPolygon(
                    new[] { center + new Vector2(-24.0f, -8.0f), center + new Vector2(0.0f, -28.0f), center + new Vector2(24.0f, -8.0f) },
                    new[] { new Color(0.62f, 0.34f, 0.24f, 1.0f) });
                break;
            case WorldSiteKind.ResourceSite:
                DrawPolygon(
                    new[] { center + new Vector2(0.0f, -27.0f), center + new Vector2(25.0f, 0.0f), center + new Vector2(0.0f, 27.0f), center + new Vector2(-25.0f, 0.0f) },
                    new[] { color });
                DrawRect(new Rect2(center - new Vector2(13.0f, 8.0f), new Vector2(26.0f, 16.0f)), new Color(0.13f, 0.12f, 0.1f, 0.92f), true);
                break;
            case WorldSiteKind.EnemySource:
                DrawCircle(center, SiteIconRadius, color);
                DrawRect(new Rect2(center - new Vector2(7.0f, 24.0f), new Vector2(14.0f, 30.0f)), new Color(0.16f, 0.14f, 0.16f, 1.0f), true);
                DrawLine(center + new Vector2(-16.0f, -5.0f), center + new Vector2(16.0f, -5.0f), new Color(0.95f, 0.45f, 0.38f, 1.0f), 3.0f, true);
                break;
            default:
                DrawCircle(center, SiteIconRadius, color);
                break;
        }
    }

    private void BuildUi()
    {
        Control hud = GameUiSceneFactory.Instantiate<Control>(
            GameUiSceneFactory.StrategicWorldHudScenePath,
            nameof(StrategicWorldRoot));
        if (hud == null)
        {
            return;
        }

        AddChild(hud);
        BindStrategicHud(hud);
        BuildMapArea();
    }

    private void BindStrategicHud(Control hud)
    {
        Label title = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TitleResourceStack/Title",
            nameof(StrategicWorldRoot));
        if (title != null)
        {
            title.Text = Definition.DisplayName;
        }

        _resourceLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TitleResourceStack/ResourceLabel",
            nameof(StrategicWorldRoot));
        _worldClockLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/WorldClockLabel",
            nameof(StrategicWorldRoot));
        _noticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/NoticeLabel",
            nameof(StrategicWorldRoot));
        _worldClockToggleButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/PauseButton",
            nameof(StrategicWorldRoot));
        _worldClockSpeedButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/QuickButton",
            nameof(StrategicWorldRoot));
        _siteTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/SiteTitleLabel",
            nameof(StrategicWorldRoot));
        _siteBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/SiteBodyLabel",
            nameof(StrategicWorldRoot));
        _siteDetailContent = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content",
            nameof(StrategicWorldRoot));
        _facilityTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/FacilityTitle",
            nameof(StrategicWorldRoot));
        _facilityList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/FacilityList",
            nameof(StrategicWorldRoot));
        _garrisonTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/GarrisonTitle",
            nameof(StrategicWorldRoot));
        _garrisonList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/GarrisonList",
            nameof(StrategicWorldRoot));
        _threatList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/ThreatList",
            nameof(StrategicWorldRoot));
        _actionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/ActionList",
            nameof(StrategicWorldRoot));
        _actionTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "SiteDetailPanel/Margin/Scroll/Content/ActionTitle",
            nameof(StrategicWorldRoot));
        _opportunityDetailPanel = GameUiSceneFactory.Instantiate<WorldOpportunityDetailPanel>(
            GameUiSceneFactory.WorldOpportunityDetailPanelScenePath,
            nameof(StrategicWorldRoot));
        if (_opportunityDetailPanel != null && _siteDetailContent != null)
        {
            _opportunityDetailPanel.Visible = false;
            _opportunityDetailPanel.CompletePressed += CompleteSelectedOpportunity;
            int insertIndex = _siteBodyLabel?.GetIndex() + 1 ?? 2;
            _siteDetailContent.AddChild(_opportunityDetailPanel);
            _siteDetailContent.MoveChild(_opportunityDetailPanel, insertIndex);
        }

        if (_worldClockToggleButton != null)
        {
            _worldClockToggleButton.Pressed += ToggleWorldClock;
        }

        if (_worldClockSpeedButton != null)
        {
            _worldClockSpeedButton.Pressed += CycleWorldClockSpeed;
        }

        Button saveButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/SaveButton",
            nameof(StrategicWorldRoot));
        Button loadButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/LoadButton",
            nameof(StrategicWorldRoot));
        Button resetButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/ResetButton",
            nameof(StrategicWorldRoot));

        if (saveButton != null)
        {
            saveButton.Pressed += SaveWorld;
        }

        if (loadButton != null)
        {
            loadButton.Pressed += LoadWorld;
        }

        if (resetButton != null)
        {
            resetButton.Pressed += ResetWorld;
        }
    }

    private void BuildMapArea()
    {
        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            Rect2 hitRect = GetSiteHitRect(site);
            Rect2 labelRect = GetSiteLabelRect(site);
            Button button = GameUiSceneFactory.CreateWorldSiteHitButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Name = $"{site.Id}Button";
            button.Position = hitRect.Position;
            button.Size = hitRect.Size;
            button.Pressed += () => SelectSite(site.Id);
            button.GuiInput += @event => OnSiteButtonGuiInput(site.Id, @event);
            AddChild(button);
            _siteButtons[site.Id] = button;

            Label label = GameUiSceneFactory.CreateWorldSiteLabel(nameof(StrategicWorldRoot));
            if (label == null)
            {
                continue;
            }

            label.Name = $"{site.Id}Label";
            label.Position = labelRect.Position;
            label.Size = labelRect.Size;
            AddChild(label);
            _siteLabels[site.Id] = label;
        }
    }

    private void ConsumeBattleResult()
    {
        if (!BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest request, out BattleResult result))
        {
            return;
        }

        WorldActionResult applyResult = _battleResultApplier.Apply(State, Definition, request, result);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        _selectedSiteId = string.IsNullOrWhiteSpace(request.TargetSiteId) ? StrategicWorldIds.SiteBonefield : request.TargetSiteId;
        _selectedThreatId = "";
        _selectedOpportunityId = "";
    }

    private void SelectSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || !State.SiteStates.ContainsKey(siteId))
        {
            return;
        }

        _selectedSiteId = siteId;
        _selectedOpportunityId = "";
        _selectedThreatId = State.SiteStates[siteId].PendingThreatIds
            .Select(id => State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .FirstOrDefault(threat => threat?.Stage == ThreatStage.Attacking)
            ?.Id ?? "";
        RefreshAll();
    }

    private void SelectThreat(string threatId)
    {
        if (string.IsNullOrWhiteSpace(threatId) || !State.ThreatPlans.TryGetValue(threatId, out EnemyThreatPlan threat))
        {
            return;
        }

        _selectedThreatId = threatId;
        _selectedSiteId = threat.TargetSiteId;
        _selectedOpportunityId = "";
        RefreshAll();
    }

    private bool TrySelectOpportunityAt(Vector2 screenPosition)
    {
        WorldOpportunityState opportunity = FindActiveOpportunityAt(screenPosition);
        if (opportunity == null)
        {
            return false;
        }

        SelectOpportunity(opportunity.OpportunityId);
        return true;
    }

    private void SelectOpportunity(string opportunityId)
    {
        if (string.IsNullOrWhiteSpace(opportunityId) ||
            !State.OpportunityStates.TryGetValue(opportunityId, out WorldOpportunityState opportunity) ||
            opportunity.Status != WorldOpportunityStatus.Active)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldOpportunityDefinition definition = queries.GetOpportunity(opportunity.DefinitionId);
        _selectedOpportunityId = opportunityId;
        _selectedThreatId = "";
        _selectedArmyIds.Clear();
        StrategicWorldRuntime.LastNotice = $"发现野外小场域：{definition?.DisplayName ?? opportunity.DefinitionId}。";
        RefreshAll();
    }

    private WorldOpportunityState FindActiveOpportunityAt(Vector2 screenPosition)
    {
        if (State?.OpportunityStates == null)
        {
            return null;
        }

        return State.OpportunityStates.Values
            .Where(opportunity => opportunity.Status == WorldOpportunityStatus.Active)
            .OrderBy(opportunity => MapToScreen(opportunity.WorldPosition).DistanceSquaredTo(screenPosition))
            .FirstOrDefault(opportunity => MapToScreen(opportunity.WorldPosition).DistanceTo(screenPosition) <= OpportunityMarkerRadius + 10.0f);
    }

    private void HandleWorldArmyInput(InputEvent @event)
    {
        if (_pendingBattleRequest != null || Definition == null || State == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                HandleWorldArmyLeftMouse(mouseButton);
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                if (_isExpeditionTargeting)
                {
                    TryIssueExpeditionToTarget(mouseButton.Position);
                    AcceptEvent();
                    return;
                }

                if (TryCommandSelectedArmies(mouseButton.Position))
                {
                    AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isArmyBoxSelecting)
        {
            _armySelectionCurrentScreen = mouseMotion.Position;
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void HandleWorldArmyLeftMouse(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed)
        {
            _isArmyBoxSelecting = true;
            _armySelectionStartScreen = mouseButton.Position;
            _armySelectionCurrentScreen = mouseButton.Position;
            AcceptEvent();
            return;
        }

        if (!_isArmyBoxSelecting)
        {
            return;
        }

        _isArmyBoxSelecting = false;
        _armySelectionCurrentScreen = mouseButton.Position;

        bool append = mouseButton.ShiftPressed;
        if (_armySelectionStartScreen.DistanceTo(_armySelectionCurrentScreen) <= 8.0f)
        {
            if (!_isExpeditionDrafting && TrySelectOpportunityAt(_armySelectionCurrentScreen))
            {
                AcceptEvent();
                return;
            }

            SelectSingleArmyAt(_armySelectionCurrentScreen, append);
        }
        else
        {
            SelectArmiesInRect(BuildScreenRect(_armySelectionStartScreen, _armySelectionCurrentScreen), append);
        }

        RefreshAll();
        AcceptEvent();
    }

    private void SelectSingleArmyAt(Vector2 screenPosition, bool append)
    {
        WorldArmyState army = FindSelectableArmyAt(screenPosition);
        if (!append)
        {
            _selectedArmyIds.Clear();
        }

        if (army == null)
        {
            StrategicWorldRuntime.LastNotice = "未选中小队。";
            return;
        }

        _selectedArmyIds.Add(army.ArmyId);
        StrategicWorldRuntime.LastNotice = $"已选中小队：{BuildArmyDisplayName(army)}。";
    }

    private void SelectArmiesInRect(Rect2 rect, bool append)
    {
        if (!append)
        {
            _selectedArmyIds.Clear();
        }

        foreach (WorldArmyState army in State.ArmyStates.Values.Where(CanSelectWorldArmy))
        {
            if (rect.HasPoint(MapToScreen(army.WorldPosition)))
            {
                _selectedArmyIds.Add(army.ArmyId);
            }
        }

        StrategicWorldRuntime.LastNotice = _selectedArmyIds.Count == 0
            ? "未圈选到小队。"
            : $"已选中 {_selectedArmyIds.Count} 支小队。";
    }

    private bool TryCommandSelectedArmies(Vector2 screenPosition)
    {
        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        WorldSiteDefinition targetSite = FindSiteAt(screenPosition);
        if (targetSite != null)
        {
            return TryCommandSelectedArmiesToSite(targetSite.Id);
        }

        Vector2 mapDestination = ScreenToMap(screenPosition);
        if (!TryBuildCommandPaths(selectedArmies, mapDestination, out Dictionary<string, StrategicNavigationPath> commandPaths, out string navigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("move", navigationFailureReason);
            return true;
        }

        foreach (WorldArmyState army in selectedArmies)
        {
            army.TargetSiteId = "";
            army.Destination = mapDestination;
            army.Intent = WorldArmyIntent.MoveToPosition;
            army.Status = WorldArmyStatus.Moving;
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
            army.SetNavigationPath(commandPaths[army.ArmyId].Points, mapDestination, _strategicNavigationContext.Version);
        }

        StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队移动。";
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandMove count={selectedArmies.Length} destination={mapDestination}");
        RefreshAll();
        return true;
    }

    private bool TryCommandSelectedArmiesToSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            return false;
        }

        if (site.OwnerFactionId == State.PlayerFactionId)
        {
            int incomingSlots = selectedArmies.Sum(army => _deploymentService.GetArmyGarrisonSlotUsage(army));
            if (!_deploymentService.CanAcceptGarrison(site, siteDefinition, incomingSlots, out string failureReason))
            {
                StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
                GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandReinforceRejected site={siteId} reason={failureReason} incoming={incomingSlots}");
                RefreshAll();
                return true;
            }

            Vector2 approachFrom = GetAverageArmyPosition(selectedArmies);
            if (!TryResolveSiteArmyNavigationPoint(siteDefinition.Id, approachFrom, out Vector2 siteArmyPosition, out Vector2 siteArrivalOffset, out WorldSiteAttackDirection siteApproachDirection, out string siteNavigationFailureReason))
            {
                ReportWorldArmyCommandNavigationRejected("reinforce_site", siteNavigationFailureReason);
                return true;
            }

            if (!TryBuildCommandPaths(selectedArmies, siteArmyPosition, out Dictionary<string, StrategicNavigationPath> commandPaths, out string navigationFailureReason))
            {
                ReportWorldArmyCommandNavigationRejected("reinforce_site", navigationFailureReason);
                return true;
            }

            CommandArmiesToSite(selectedArmies, siteDefinition, siteArmyPosition, siteArrivalOffset, siteApproachDirection, WorldArmyIntent.ReinforceSite, commandPaths);
            StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进驻 {siteDefinition.DisplayName}。";
            RefreshAll();
            return true;
        }

        if (!CanBuildAssaultBattleForSite(siteDefinition.Id))
        {
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandAssaultRejected site={siteDefinition.Id} reason=missing_assault_battle_config");
            RefreshAll();
            return true;
        }

        Vector2 assaultApproachFrom = GetAverageArmyPosition(selectedArmies);
        if (!TryResolveSiteArmyNavigationPoint(siteDefinition.Id, assaultApproachFrom, out Vector2 assaultSiteArmyPosition, out Vector2 assaultArrivalOffset, out WorldSiteAttackDirection assaultApproachDirection, out string assaultSiteNavigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("assault_site", assaultSiteNavigationFailureReason);
            return true;
        }

        if (!TryBuildCommandPaths(selectedArmies, assaultSiteArmyPosition, out Dictionary<string, StrategicNavigationPath> commandPathsToSite, out string navigationFailureReasonToSite))
        {
            ReportWorldArmyCommandNavigationRejected("assault_site", navigationFailureReasonToSite);
            return true;
        }

        CommandArmiesToSite(selectedArmies, siteDefinition, assaultSiteArmyPosition, assaultArrivalOffset, assaultApproachDirection, WorldArmyIntent.AssaultSite, commandPathsToSite);
        StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进攻 {siteDefinition.DisplayName}。";
        RefreshAll();
        return true;
    }

    private void CommandArmiesToSite(
        WorldArmyState[] armies,
        WorldSiteDefinition siteDefinition,
        Vector2 siteArmyPosition,
        Vector2 arrivalApproachOffset,
        WorldSiteAttackDirection approachDirection,
        WorldArmyIntent intent,
        IReadOnlyDictionary<string, StrategicNavigationPath> commandPaths)
    {
        foreach (WorldArmyState army in armies)
        {
            army.TargetSiteId = siteDefinition.Id;
            army.Destination = siteArmyPosition;
            army.Intent = intent;
            army.Status = WorldArmyStatus.Moving;
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
            army.SetNavigationPath(commandPaths[army.ArmyId].Points, siteArmyPosition, _strategicNavigationContext.Version);
        }

        GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandSite count={armies.Length} target={siteDefinition.Id} intent={intent} approachDirection={approachDirection}");
    }

    private bool TryBuildCommandPaths(
        IReadOnlyList<WorldArmyState> armies,
        Vector2 destination,
        out Dictionary<string, StrategicNavigationPath> commandPaths,
        out string failureReason)
    {
        commandPaths = new Dictionary<string, StrategicNavigationPath>();
        failureReason = "";
        if (armies == null || armies.Count == 0)
        {
            failureReason = "no_commandable_army";
            return false;
        }

        if (_strategicNavigationContext == null)
        {
            failureReason = "strategic_navigation_context_missing";
            return false;
        }

        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            if (!_strategicNavigationContext.TryBuildPath(army.WorldPosition, destination, out StrategicNavigationPath path, out string pathFailureReason))
            {
                commandPaths.Clear();
                failureReason = $"army={army.ArmyId} {pathFailureReason}";
                return false;
            }

            commandPaths[army.ArmyId] = path;
        }

        if (commandPaths.Count == 0)
        {
            failureReason = "no_commandable_army";
            return false;
        }

        return true;
    }

    private static Vector2 GetAverageArmyPosition(IReadOnlyList<WorldArmyState> armies)
    {
        if (armies == null || armies.Count == 0)
        {
            return Vector2.Zero;
        }

        Vector2 sum = Vector2.Zero;
        int count = 0;
        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            sum += army.WorldPosition;
            count++;
        }

        return count == 0 ? Vector2.Zero : sum / count;
    }

    private void ReportWorldArmyCommandNavigationRejected(string commandKind, string failureReason)
    {
        StrategicWorldRuntime.LastNotice = failureReason?.Contains("start_", System.StringComparison.Ordinal) == true
            ? "部队当前位置不在可行军区域，无法行军。"
            : "目标地点不在可行军区域，无法行军。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"WorldArmyCommandNavigationRejected kind={commandKind} reason={failureReason}");
        RefreshAll();
    }

    private bool TryResolveSiteArmyNavigationPoint(string siteId, out Vector2 mapPosition, out string failureReason)
    {
        return TryResolveSiteArmyNavigationPoint(siteId, null, out mapPosition, out _, out _, out failureReason);
    }

    private bool TryResolveSiteArmyNavigationPoint(
        string siteId,
        Vector2? approachFrom,
        out Vector2 mapPosition,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out string failureReason)
    {
        mapPosition = default;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        failureReason = "";
        if (Definition == null || _worldMapRoot == null)
        {
            failureReason = "strategic_world_not_ready";
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            failureReason = $"missing_site_definition site={siteId}";
            return false;
        }

        if (_armySpawnPointRoot?.GetNodeOrNull<Node2D>(siteId) is { } spawnPoint)
        {
            mapPosition = _worldMapRoot.ToLocal(spawnPoint.GlobalPosition);
            if (_strategicNavigationContext.IsPointNavigable(mapPosition, out failureReason))
            {
                return true;
            }

            failureReason = $"site_army_spawn_point_not_navigable site={siteId} {failureReason}";
            return false;
        }

        if (approachFrom is { } sourcePosition &&
            TryResolveSiteFootprintApproachPoint(siteDefinition, sourcePosition, out mapPosition, out arrivalApproachOffset, out approachDirection, out _))
        {
            ReportSiteNavigationPointResolved(siteId, GetSiteMapPosition(siteDefinition), mapPosition);
            return true;
        }

        Vector2 siteCenter = GetSiteMapPosition(siteDefinition);
        if (!_strategicNavigationContext.TryGetNearestNavigablePoint(
                siteCenter,
                SiteNavigationPointSearchCellRadius,
                out mapPosition,
                out failureReason))
        {
            failureReason = $"site_navigation_point_missing site={siteId} {failureReason}";
            return false;
        }

        if (siteCenter.DistanceSquaredTo(mapPosition) > 0.001f)
        {
            ReportSiteNavigationPointResolved(siteId, siteCenter, mapPosition);
        }

        return true;
    }

    private bool TryResolveSiteExitArmyNavigationPoint(
        string siteId,
        Vector2 towardPosition,
        out Vector2 mapPosition,
        out string failureReason)
    {
        return TryResolveSiteArmyNavigationPoint(siteId, towardPosition, out mapPosition, out _, out _, out failureReason);
    }

    private bool TryResolveSiteFootprintApproachPoint(
        WorldSiteDefinition siteDefinition,
        Vector2 approachFrom,
        out Vector2 mapPosition,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out string failureReason)
    {
        mapPosition = default;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        failureReason = "";
        if (siteDefinition == null ||
            _siteVisualLayer == null ||
            !_siteVisualFootprints.TryGetValue(siteDefinition.Id, out SiteVisualFootprint footprint))
        {
            failureReason = "site_visual_footprint_missing";
            return false;
        }

        Vector2 siteCenter = GetSiteMapPosition(siteDefinition);
        Vector2 direction = siteCenter - approachFrom;
        if (!IsFinite(approachFrom) || direction.LengthSquared() <= 0.001f)
        {
            failureReason = "site_approach_direction_invalid";
            return false;
        }

        direction = direction.Normalized();
        if (!TryFindSiteFootprintEdgePoint(footprint, approachFrom, siteCenter, out Vector2 edgePoint))
        {
            failureReason = "site_footprint_edge_missing";
            return false;
        }

        approachDirection = ResolveFootprintApproachDirection(footprint, edgePoint);

        Vector2 searchPoint = edgePoint - direction * SiteApproachEdgeNudge;
        if (!_strategicNavigationContext.TryGetNearestNavigablePoint(
                searchPoint,
                SiteNavigationPointSearchCellRadius,
                out mapPosition,
                out failureReason))
        {
            failureReason = $"site_approach_navigation_missing site={siteDefinition.Id} {failureReason}";
            return false;
        }

        arrivalApproachOffset = direction * SiteApproachVisualOffset;
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteApproachNavigationPointResolved site={siteDefinition.Id} from={approachFrom} edge={edgePoint} navigation={mapPosition} arrivalOffset={arrivalApproachOffset} approachDirection={approachDirection}");
        return true;
    }

    private bool TryFindSiteFootprintEdgePoint(
        SiteVisualFootprint footprint,
        Vector2 approachFrom,
        Vector2 siteCenter,
        out Vector2 edgePoint)
    {
        edgePoint = default;
        if (footprint == null || _siteVisualLayer == null)
        {
            return false;
        }

        bool hasIntersection = false;
        float bestSegmentRatio = float.PositiveInfinity;
        foreach (Vector2I cell in footprint.Cells)
        {
            Vector2[] polygon = BuildTileCellMapPolygon(_siteVisualLayer, cell);
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 edgeStart = polygon[index];
                Vector2 edgeEnd = polygon[(index + 1) % polygon.Length];
                if (!TryIntersectSegments(
                        approachFrom,
                        siteCenter,
                        edgeStart,
                        edgeEnd,
                        out float segmentRatio,
                        out Vector2 intersection) ||
                    segmentRatio >= bestSegmentRatio)
                {
                    continue;
                }

                bestSegmentRatio = segmentRatio;
                edgePoint = intersection;
                hasIntersection = true;
            }
        }

        return hasIntersection;
    }

    private static WorldSiteAttackDirection ResolveFootprintApproachDirection(
        SiteVisualFootprint footprint,
        Vector2 edgePoint)
    {
        if (footprint == null || footprint.MapBounds.Size == Vector2.Zero)
        {
            return WorldSiteAttackDirection.Any;
        }

        Rect2 bounds = footprint.MapBounds;
        float leftDistance = Mathf.Abs(edgePoint.X - bounds.Position.X);
        float rightDistance = Mathf.Abs(edgePoint.X - bounds.End.X);
        float topDistance = Mathf.Abs(edgePoint.Y - bounds.Position.Y);
        float bottomDistance = Mathf.Abs(edgePoint.Y - bounds.End.Y);
        float best = Mathf.Min(Mathf.Min(leftDistance, rightDistance), Mathf.Min(topDistance, bottomDistance));
        if (Mathf.Abs(best - leftDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.West;
        }

        if (Mathf.Abs(best - rightDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.East;
        }

        if (Mathf.Abs(best - topDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.North;
        }

        return WorldSiteAttackDirection.South;
    }

    private void ResolveMovingArmySiteNavigationPoints()
    {
        if (State == null || Definition == null || _strategicNavigationContext == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.Status != WorldArmyStatus.Moving || army.HasNavigationPath || army.IsCompletingArrivalApproach)
            {
                continue;
            }

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(army.SourceSiteId) &&
                queries.GetSite(army.SourceSiteId) is { } sourceSite &&
                army.WorldPosition.DistanceSquaredTo(GetSiteMapPosition(sourceSite)) <= SiteNavigationPointSnapDistance * SiteNavigationPointSnapDistance &&
                TryResolveSiteExitArmyNavigationPoint(army.SourceSiteId, army.Destination, out Vector2 sourcePosition, out _))
            {
                if (army.WorldPosition.DistanceSquaredTo(sourcePosition) > 0.001f)
                {
                    army.WorldPosition = sourcePosition;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(army.TargetSiteId) &&
                TryResolveSiteArmyNavigationPoint(army.TargetSiteId, army.WorldPosition, out Vector2 destinationPosition, out Vector2 arrivalApproachOffset, out WorldSiteAttackDirection approachDirection, out _))
            {
                if (army.Destination.DistanceSquaredTo(destinationPosition) > 0.001f)
                {
                    army.Destination = destinationPosition;
                    army.SetArrivalApproachOffset(arrivalApproachOffset);
                    changed = true;
                }

                if (army.TargetApproachDirection != approachDirection)
                {
                    army.SetTargetApproachDirection(approachDirection);
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            army.ClearNavigationPath();
            GameLog.Info(
                nameof(StrategicWorldRoot),
                $"WorldArmySiteNavigationPointsResolved army={army.ArmyId} source={army.SourceSiteId} target={army.TargetSiteId} position={army.WorldPosition} destination={army.Destination}");
        }
    }

    private void ReportSiteNavigationPointResolved(string siteId, Vector2 siteCenter, Vector2 navigationPoint)
    {
        if (!_reportedSiteNavigationPointResolutions.Add(siteId))
        {
            return;
        }

        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteNavigationPointResolved site={siteId} center={siteCenter} point={navigationPoint} source=StrategicNavigationTileLayer");
    }

    private void OnSiteButtonGuiInput(string siteId, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            return;
        }

        if (_isExpeditionTargeting)
        {
            TryIssueExpeditionToSite(siteId);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_selectedArmyIds.Count == 0)
        {
            return;
        }

        TryCommandSelectedArmiesToSite(siteId);
        GetViewport().SetInputAsHandled();
    }

    private void BeginExpeditionDraft()
    {
        if (!CanStartExpeditionFromSite(_selectedSiteId, out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return;
        }

        _isExpeditionDrafting = true;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = _selectedSiteId;
        _expeditionUnitCounts.Clear();
        foreach ((string unitTypeId, int available) in GetAvailableExpeditionUnits(_expeditionSourceSiteId))
        {
            if (available > 0)
            {
                _expeditionUnitCounts[unitTypeId] = 1;
            }
        }
        ClampExpeditionDraftCounts();
        StrategicWorldRuntime.LastNotice = "选择出征英雄和小兵。";
        RefreshAll();
    }

    private void BeginExpeditionTargeting()
    {
        ClampExpeditionDraftCounts();
        if (!HasSelectedExpeditionUnits())
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄或小兵。";
            RefreshAll();
            return;
        }

        _isExpeditionTargeting = true;
        _selectedArmyIds.Clear();
        StrategicWorldRuntime.LastNotice = "选择出征目的地：右键敌方场域为进攻，右键己方场域为进驻，右键空地为移动。";
        RefreshAll();
    }

    private void CancelExpeditionDraft()
    {
        _isExpeditionDrafting = false;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = "";
        _expeditionUnitCounts.Clear();
        StrategicWorldRuntime.LastNotice = "已取消出征。";
        RefreshAll();
    }

    private void AdjustExpeditionUnitCount(string unitTypeId, int delta)
    {
        if (string.IsNullOrWhiteSpace(unitTypeId))
        {
            return;
        }

        int available = GetAvailableUnitCount(_expeditionSourceSiteId, unitTypeId);
        _expeditionUnitCounts.TryGetValue(unitTypeId, out int selected);
        selected = System.Math.Clamp(selected + delta, 0, available);
        if (selected <= 0)
        {
            _expeditionUnitCounts.Remove(unitTypeId);
        }
        else
        {
            _expeditionUnitCounts[unitTypeId] = selected;
        }

        RefreshAll();
    }

    private bool TryIssueExpeditionToTarget(Vector2 screenPosition)
    {
        WorldSiteDefinition targetSite = FindSiteAt(screenPosition);
        if (targetSite != null)
        {
            return TryIssueExpeditionToSite(targetSite.Id);
        }

        return TryCreateExpedition("", ScreenToMap(screenPosition), WorldArmyIntent.MoveToPosition);
    }

    private bool TryIssueExpeditionToSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            return false;
        }

        if (siteId == _expeditionSourceSiteId)
        {
            StrategicWorldRuntime.LastNotice = "出征目标不能是出发场域。";
            RefreshAll();
            return true;
        }

        if (site.OwnerFactionId == State.PlayerFactionId)
        {
            return TryCreateExpedition(siteId, siteDefinition.MapPosition, WorldArmyIntent.ReinforceSite);
        }

        if (!CanBuildAssaultBattleForSite(siteId))
        {
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            RefreshAll();
            return true;
        }

        return TryCreateExpedition(siteId, siteDefinition.MapPosition, WorldArmyIntent.AssaultSite);
    }

    private bool TryCreateExpedition(string targetSiteId, Vector2 destination, WorldArmyIntent intent)
    {
        ClampExpeditionDraftCounts();
        Dictionary<string, int> units = BuildSelectedExpeditionUnits();
        if (units.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄或小兵。";
            RefreshAll();
            return true;
        }

        if (!TryResolveExpeditionNavigation(
                targetSiteId,
                destination,
                out Vector2 sourceArmyPosition,
                out Vector2 resolvedDestination,
                out Vector2 arrivalApproachOffset,
                out WorldSiteAttackDirection approachDirection,
                out StrategicNavigationPath expeditionPath,
                out string navigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("expedition", navigationFailureReason);
            return true;
        }

        if (!_expeditionService.TryCreateExpedition(
                State,
                Definition,
                _expeditionSourceSiteId,
                sourceArmyPosition,
                targetSiteId,
                resolvedDestination,
                intent,
                units,
                out WorldArmyState army,
                out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return true;
        }

        army.SetNavigationPath(expeditionPath.Points, army.Destination, _strategicNavigationContext.Version);
        if (intent == WorldArmyIntent.MoveToPosition)
        {
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
        }
        else
        {
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
        }

        _selectedArmyIds.Clear();
        _selectedArmyIds.Add(army.ArmyId);
        string sourceName = ResolveSiteDisplayName(_expeditionSourceSiteId);
        string targetText = string.IsNullOrWhiteSpace(targetSiteId)
            ? "目标地点"
            : ResolveSiteDisplayName(targetSiteId);
        StrategicWorldRuntime.LastNotice = intent switch
        {
            WorldArmyIntent.AssaultSite => $"已从{sourceName}派出{BuildExpeditionUnitText()}进攻{targetText}。",
            WorldArmyIntent.ReinforceSite => $"已从{sourceName}派出{BuildExpeditionUnitText()}进驻{targetText}。",
            _ => $"已从{sourceName}派出{BuildExpeditionUnitText()}移动到目标地点。"
        };
        _isExpeditionDrafting = false;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = "";
        _expeditionUnitCounts.Clear();
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldExpeditionIssued army={army.ArmyId} intent={intent} target={targetSiteId}");
        RefreshAll();
        return true;
    }

    private bool TryResolveExpeditionNavigation(
        string targetSiteId,
        Vector2 requestedDestination,
        out Vector2 sourceArmyPosition,
        out Vector2 resolvedDestination,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        sourceArmyPosition = default;
        resolvedDestination = requestedDestination;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        path = null;
        failureReason = "";
        if (_strategicNavigationContext == null)
        {
            failureReason = "strategic_navigation_context_missing";
            return false;
        }

        if (!TryResolveSiteExitArmyNavigationPoint(_expeditionSourceSiteId, requestedDestination, out sourceArmyPosition, out failureReason))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(targetSiteId) &&
            !TryResolveSiteArmyNavigationPoint(targetSiteId, sourceArmyPosition, out resolvedDestination, out arrivalApproachOffset, out approachDirection, out failureReason))
        {
            return false;
        }

        return _strategicNavigationContext.TryBuildPath(sourceArmyPosition, resolvedDestination, out path, out failureReason);
    }

    private bool IsSiteBlockedForExpeditionTarget(string siteId)
    {
        if (!_isExpeditionTargeting ||
            string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        if (siteId == _expeditionSourceSiteId)
        {
            return true;
        }

        if (site.OwnerFactionId != State.PlayerFactionId)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        return !_deploymentService.CanAcceptGarrison(site, queries.GetSite(siteId), GetSelectedExpeditionUnitCount(), out _);
    }

    private bool CanStartExpeditionFromSite(string siteId, out string failureReason)
    {
        failureReason = "";
        if (HasAttackingThreat())
        {
            failureReason = "attacking_threat_pending";
            return false;
        }

        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            failureReason = "missing_source_site";
            return false;
        }

        if (site.OwnerFactionId != State.PlayerFactionId ||
            site.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
        {
            failureReason = "source_site_not_owned";
            return false;
        }

        if (GetAvailableExpeditionUnitCount(siteId) <= 0)
        {
            failureReason = "no_expedition_units";
            return false;
        }

        return true;
    }

    private void ClampExpeditionDraftCounts()
    {
        Dictionary<string, int> availableUnits = GetAvailableExpeditionUnits(_expeditionSourceSiteId);
        foreach (string unitTypeId in _expeditionUnitCounts.Keys.ToArray())
        {
            int available = availableUnits.TryGetValue(unitTypeId, out int count) ? count : 0;
            int selected = System.Math.Clamp(_expeditionUnitCounts[unitTypeId], 0, available);
            if (selected <= 0)
            {
                _expeditionUnitCounts.Remove(unitTypeId);
            }
            else
            {
                _expeditionUnitCounts[unitTypeId] = selected;
            }
        }
    }

    private bool HasSelectedExpeditionUnits()
    {
        return GetSelectedExpeditionUnitCount() > 0;
    }

    private int GetSelectedExpeditionUnitCount()
    {
        return _expeditionUnitCounts.Values.Sum(count => System.Math.Max(count, 0));
    }

    private int GetAvailableExpeditionUnitCount(string siteId)
    {
        return GetAvailableExpeditionUnits(siteId).Values.Sum(count => System.Math.Max(count, 0));
    }

    private Dictionary<string, int> GetAvailableExpeditionUnits(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site) ||
            site.Garrison == null)
        {
            return new Dictionary<string, int>();
        }

        return site.Garrison
            .Where(unit => !string.IsNullOrWhiteSpace(unit.UnitTypeId) && unit.Count > 0)
            .GroupBy(unit => unit.UnitTypeId)
            .OrderBy(group => GetUnitSortKey(group.Key))
            .ThenBy(group => GetUnitLabel(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(unit => System.Math.Max(unit.Count, 0)));
    }

    private int GetAvailableUnitCount(string siteId, string unitTypeId)
    {
        return !string.IsNullOrWhiteSpace(siteId) &&
               !string.IsNullOrWhiteSpace(unitTypeId) &&
               State.SiteStates.TryGetValue(siteId, out WorldSiteState site)
            ? site.Garrison
                .Where(unit => unit.UnitTypeId == unitTypeId)
                .Sum(unit => System.Math.Max(unit.Count, 0))
            : 0;
    }

    private Dictionary<string, int> BuildSelectedExpeditionUnits()
    {
        return _expeditionUnitCounts
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value > 0)
            .ToDictionary(item => item.Key, item => item.Value);
    }

    private static int GetSiteUnitCount(WorldSiteState state)
    {
        return state?.Garrison?
            .Sum(unit => System.Math.Max(unit.Count, 0)) ?? 0;
    }

    private static int GetSiteHeroCount(WorldSiteState state)
    {
        int tagHeroes = state?.ActiveTags?.Count(tag => tag.StartsWith("hero:")) ?? 0;
        int unitHeroes = state?.Garrison?
            .Where(unit => IsHeroUnitType(unit.UnitTypeId))
            .Sum(unit => System.Math.Max(unit.Count, 0)) ?? 0;
        return tagHeroes + unitHeroes;
    }

    private static int GetSiteArmyCount(WorldSiteState state)
    {
        return state?.Garrison == null
            ? 0
            : state.Garrison
                .Where(unit => !IsHeroUnitType(unit.UnitTypeId))
                .Sum(unit => System.Math.Max(unit.Count, 0));
    }

    private string BuildExpeditionUnitText()
    {
        Dictionary<string, int> selectedUnits = BuildSelectedExpeditionUnits();
        if (selectedUnits.Count > 0)
        {
            return string.Join("、", selectedUnits
                .OrderBy(item => GetUnitSortKey(item.Key))
                .ThenBy(item => GetUnitLabel(item.Key))
                .Select(item => $"{GetUnitLabel(item.Key)} x{item.Value}"));
        }

        int _expeditionHeroCount = 0;
        int _expeditionMilitiaCount = 0;
        List<string> parts = new();
        if (_expeditionHeroCount > 0)
        {
            parts.Add($"{GetUnitLabel(StrategicWorldIds.UnitPlayerKnight)} x{_expeditionHeroCount}");
        }

        if (_expeditionMilitiaCount > 0)
        {
            parts.Add($"{GetUnitLabel(StrategicWorldIds.UnitMilitia)} x{_expeditionMilitiaCount}");
        }

        return parts.Count == 0 ? "未选择单位" : string.Join("、", parts);
    }

    private static bool IsHeroUnitType(string unitTypeId)
    {
        return unitTypeId == StrategicWorldIds.UnitPlayerKnight;
    }

    private static int GetUnitSortKey(string unitTypeId)
    {
        return unitTypeId switch
        {
            StrategicWorldIds.UnitPlayerKnight => 0,
            StrategicWorldIds.UnitMilitia => 10,
            _ => 20
        };
    }

    private bool IsSiteBlockedForSelectedSiteCommand(string siteId)
    {
        if (_selectedArmyIds.Count == 0 ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        if (site.OwnerFactionId != State.PlayerFactionId)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        int incomingSlots = selectedArmies.Sum(army => _deploymentService.GetArmyGarrisonSlotUsage(army));
        return !_deploymentService.CanAcceptGarrison(site, queries.GetSite(siteId), incomingSlots, out _);
    }

    private WorldArmyState[] GetSelectedCommandableArmies()
    {
        return _selectedArmyIds
            .Select(id => State.ArmyStates.TryGetValue(id, out WorldArmyState army) ? army : null)
            .Where(CanCommandWorldArmy)
            .ToArray();
    }

    private WorldArmyState FindSelectableArmyAt(Vector2 screenPosition)
    {
        return State.ArmyStates.Values
            .Where(CanSelectWorldArmy)
            .OrderBy(army => MapToScreen(army.WorldPosition).DistanceSquaredTo(screenPosition))
            .FirstOrDefault(army => MapToScreen(army.WorldPosition).DistanceTo(screenPosition) <= Mathf.Max(army.Radius + 10.0f, 24.0f));
    }

    private WorldSiteDefinition FindSiteAt(Vector2 screenPosition)
    {
        return Definition.SiteDefinitions
            .Where(site => State.SiteStates.ContainsKey(site.Id))
            .FirstOrDefault(site =>
            {
                return GetSiteHitRect(site).HasPoint(screenPosition);
            });
    }

    private static bool CanSelectWorldArmy(WorldArmyState army)
    {
        return army != null &&
               army.OwnerFactionId == StrategicWorldIds.FactionPlayer &&
               army.Status is not (WorldArmyStatus.Defeated or WorldArmyStatus.Garrisoned);
    }

    private static bool CanCommandWorldArmy(WorldArmyState army)
    {
        return CanSelectWorldArmy(army) &&
               army.Status != WorldArmyStatus.Attacking;
    }

    private string BuildArmyDisplayName(WorldArmyState army)
    {
        if (army == null)
        {
            return "未知小队";
        }

        int unitCount = army.GarrisonUnits.Sum(unit => unit.Count);
        return unitCount > 0 ? $"{army.ArmyId} ({unitCount})" : army.ArmyId;
    }

    private string ResolveSiteDisplayName(string siteId)
    {
        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition definition = queries.GetSite(siteId);
        return string.IsNullOrWhiteSpace(definition?.DisplayName) ? siteId : definition.DisplayName;
    }

    private void RefreshAll()
    {
        StrategicWorldDefinitionQueries queries = new(Definition);
        RefreshResources();
        RefreshSiteButtons(queries);
        RefreshDetail(queries);
        RefreshThreats(queries);
        RefreshActions();
        RefreshWorldClockLabel();
        _noticeLabel.Text = StrategicWorldRuntime.LastNotice;
        QueueRedraw();
    }

    private void RefreshResources()
    {
        ResourceStore resources = State.PlayerResources;
        _resourceLabel.Text =
            $"人口 {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}    " +
            $"经济 {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}    " +
            $"石材 {resources.GetAmount(StrategicWorldIds.ResourceStone)}    " +
            $"世界步 {State.WorldTick}";
    }

    private void RefreshSiteButtons(StrategicWorldDefinitionQueries queries)
    {
        foreach ((string siteId, Button button) in _siteButtons)
        {
            WorldSiteState state = State.SiteStates[siteId];
            WorldSiteDefinition definition = queries.GetSite(siteId);
            Rect2 hitRect = GetSiteHitRect(definition);
            button.Position = hitRect.Position;
            button.Size = hitRect.Size;
            string threatMark = state.PendingThreatIds
                .Select(id => State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
                .Any(threat => threat is { Stage: ThreatStage.Attacking })
                ? "\n进攻中"
                : "";
            int unitCount = GetSiteUnitCount(state);
            int hero = 0;
            int garrison = unitCount;
            int facilities = state.Facilities.Count(item => item.State != FacilityState.Destroyed);
            button.Text = "";
            bool isBlockedTarget = _isExpeditionTargeting
                ? IsSiteBlockedForExpeditionTarget(siteId)
                : IsSiteBlockedForSelectedSiteCommand(siteId);
            button.MouseDefaultCursorShape = isBlockedTarget
                ? CursorShape.Forbidden
                : CursorShape.PointingHand;
            button.TooltipText = $"{definition.DisplayName}\n{GetControlStateLabel(state.ControlState)}  建筑 {facilities}  英雄 {hero}  兵团 {garrison}{threatMark.Replace("\n", " ")}";

            if (_siteLabels.TryGetValue(siteId, out Label label))
            {
                Rect2 labelRect = GetSiteLabelRect(definition);
                label.Position = labelRect.Position;
                label.Size = labelRect.Size;
                label.Text = string.IsNullOrWhiteSpace(threatMark)
                    ? definition.DisplayName
                    : $"{definition.DisplayName}{threatMark}";
                label.TooltipText = button.TooltipText;
            }

        }
    }

    private void RefreshDetail(StrategicWorldDefinitionQueries queries)
    {
        if (TryRefreshOpportunityDetail(queries))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedSiteId) || !State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site))
        {
            _selectedSiteId = Definition.StartingSiteId;
            if (!State.SiteStates.TryGetValue(_selectedSiteId, out site))
            {
                return;
            }
        }

        SetSiteDetailSectionsVisible(true);
        WorldSiteDefinition definition = queries.GetSite(_selectedSiteId);
        _siteTitleLabel.Text = $"{definition.DisplayName}  ·  {GetSiteKindLabel(definition.SiteKind)}";
        _siteBodyLabel.Text =
            $"{definition.Description}\n\n" +
            $"状态：{GetControlStateLabel(site.ControlState)}\n" +
            $"模式：{GetSiteModeLabel(site.SiteMode)}\n" +
            $"归属：{GetFactionLabel(site.OwnerFactionId)}\n" +
            $"受损：{site.DamageLevel}";

        ClearChildren(_facilityList);
        if (site.Facilities.Count == 0)
        {
            AddMutedLine(_facilityList, "无");
        }
        else
        {
            foreach (FacilityInstance facility in site.Facilities)
            {
                FacilityDefinition facilityDefinition = queries.GetFacility(facility.FacilityId);
                string extra = facility.FacilityId == StrategicWorldIds.FacilityMine
                    ? $"  占用人口 {facility.AssignedPopulation}  产出 石材 +2/世界步"
                    : facility.FacilityId == StrategicWorldIds.FacilityDefenseTower
                        ? "  防守 +3  塔支援 1 次"
                        : "";
                AddMutedLine(_facilityList, $"{facilityDefinition?.DisplayName ?? facility.FacilityId}  {GetFacilityStateLabel(facility.State)}{extra}");
            }
        }

        ClearChildren(_garrisonList);
        if (site.Garrison.Count == 0)
        {
            AddMutedLine(_garrisonList, "无");
        }
        else
        {
            foreach (GarrisonState garrison in site.Garrison)
            {
                AddMutedLine(_garrisonList, $"{GetUnitLabel(garrison.UnitTypeId)} x{garrison.Count}");
            }
        }
    }

    private bool TryRefreshOpportunityDetail(StrategicWorldDefinitionQueries queries)
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            return false;
        }

        if (_opportunityDetailPanel == null)
        {
            _selectedOpportunityId = "";
            GameLog.Warn(nameof(StrategicWorldRoot), "Missing WorldOpportunityDetailPanel scene instance.");
            return false;
        }

        WorldOpportunityDefinition definition = queries.GetOpportunity(opportunity.DefinitionId);
        OpportunitySpawnPointDefinition spawnPoint = queries.GetOpportunitySpawnPoint(opportunity.SpawnPointId);
        int remainingTicks = System.Math.Max(0, opportunity.ExpiresTick - State.WorldTick);
        SetSiteDetailSectionsVisible(false);
        _opportunityDetailPanel.Visible = true;
        _opportunityDetailPanel.Bind(new WorldOpportunityDetailPanelData
        {
            Title = $"野外小场域 · {definition?.DisplayName ?? opportunity.DefinitionId}",
            Description = definition?.Description ?? "临时出现的野外机会。",
            StatusText = GetOpportunityStatusLabel(opportunity.Status),
            SpawnPointText = spawnPoint?.DisplayName ?? opportunity.SpawnPointId,
            RemainingText = $"{remainingTicks} 世界步",
            RewardText = BuildOpportunityRewardText(definition)
        });
        return true;
    }

    private void SetSiteDetailSectionsVisible(bool visible)
    {
        if (_siteTitleLabel != null)
        {
            _siteTitleLabel.Visible = visible;
        }

        if (_siteBodyLabel != null)
        {
            _siteBodyLabel.Visible = visible;
        }

        if (_facilityTitleLabel != null)
        {
            _facilityTitleLabel.Visible = visible;
        }

        if (_facilityList != null)
        {
            _facilityList.Visible = visible;
        }

        if (_garrisonTitleLabel != null)
        {
            _garrisonTitleLabel.Visible = visible;
        }

        if (_garrisonList != null)
        {
            _garrisonList.Visible = visible;
        }

        if (_actionTitleLabel != null)
        {
            _actionTitleLabel.Visible = visible;
        }

        if (_actionList != null)
        {
            _actionList.Visible = visible;
        }

        if (_opportunityDetailPanel != null)
        {
            _opportunityDetailPanel.Visible = !visible;
        }
    }

    private void RefreshThreats(StrategicWorldDefinitionQueries queries)
    {
        ClearChildren(_threatList);
        EnemyThreatPlan[] activeThreats = State.ThreatPlans.Values
            .Where(threat => threat.Stage != ThreatStage.Resolved)
            .OrderBy(threat => threat.Stage == ThreatStage.Attacking ? 0 : 1)
            .ThenBy(threat => threat.CountdownTicks)
            .ToArray();

        if (activeThreats.Length == 0)
        {
            AddMutedLine(_threatList, "暂无");
            return;
        }

        foreach (EnemyThreatPlan threat in activeThreats)
        {
            string source = queries.GetSite(threat.SourceSiteId)?.DisplayName ?? threat.SourceSiteId;
            string target = queries.GetSite(threat.TargetSiteId)?.DisplayName ?? threat.TargetSiteId;
            string movingText = !string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                                State.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army) &&
                                army.Status == WorldArmyStatus.Moving
                ? BuildArmyArrivalText(army)
                : $"{threat.CountdownTicks} 步后到达";
            WorldBattleState worldBattle = WorldBattleProgressionService.FindActiveBattleForThreat(State, threat.Id);
            Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = worldBattle != null
                ? $"世界战斗：{source} -> {target}  {WorldBattleProgressionService.GetPhaseLabel(worldBattle.CurrentPhase)}  剩余 {WorldBattleProgressionService.GetRemainingTicks(State, worldBattle)} 步"
                : threat.Stage == ThreatStage.Attacking
                ? $"亡灵袭击：{source} -> {target}  正在攻击"
                : $"亡灵袭击：{source} -> {target}  {movingText}";
            button.Disabled = false;
            button.Pressed += () => SelectThreat(threat.Id);
            _threatList.AddChild(button);

            if (worldBattle != null)
            {
                AddWorldBattleInterventionButton(worldBattle);
            }
        }
    }

    private void AddWorldBattleInterventionButton(WorldBattleState worldBattle)
    {
        if (worldBattle == null)
        {
            return;
        }

        Button interveneButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (interveneButton == null)
        {
            return;
        }

        interveneButton.Text =
            $"介入战斗\n{WorldBattleProgressionService.GetOutcomeLabel(worldBattle.ProjectedOutcome)}  {WorldBattleProgressionService.GetPhaseLabel(worldBattle.CurrentPhase)}";
        interveneButton.Disabled = false;
        interveneButton.Pressed += () => TryEnterWorldBattleIntervention(worldBattle.BattleId);
        _threatList.AddChild(interveneButton);
    }

    private string BuildArmyArrivalText(WorldArmyState army)
    {
        float remainingDistance = army.WorldPosition.DistanceTo(army.Destination);
        double movementSpeed = System.Math.Max(1.0, army.MoveSpeed * WorldClockSpeedMultipliers[_worldClockSpeedIndex]);
        double etaSeconds = System.Math.Ceiling(remainingDistance / movementSpeed);
        return $"敌军行军中  预计 {etaSeconds:0}s 抵达";
    }

    private void RefreshActions()
    {
        ClearChildren(_actionList);
        if (RefreshExpeditionControls())
        {
            return;
        }

        if (TryGetSelectedActiveOpportunity(out _))
        {
            return;
        }

        IReadOnlyList<WorldActionViewModel> actions = _actionResolver.GetAvailableActions(State, Definition, _selectedSiteId, _selectedThreatId);
        foreach (WorldActionViewModel action in actions)
        {
            Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = BuildActionButtonText(action);
            button.Disabled = !action.IsEnabled;

            if (action.IsEnabled)
            {
                button.Pressed += () => ExecuteAction(action);
            }

            _actionList.AddChild(button);
        }

        if (_actionList.GetChildCount() == 0)
        {
            AddMutedLine(_actionList, "暂无可执行行动");
        }
    }

    private void CompleteSelectedOpportunity()
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            StrategicWorldRuntime.LastNotice = "野外小场域已经消失。";
            _selectedOpportunityId = "";
            RefreshAll();
            return;
        }

        WorldActionResult result = _opportunityService.CompleteOpportunity(State, Definition, opportunity.OpportunityId);
        StrategicWorldRuntime.LastNotice = result.Message;
        if (result.Success)
        {
            _selectedOpportunityId = "";
        }

        RefreshAll();
    }

    private bool RefreshExpeditionControls()
    {
        WorldSiteState selectedSite = State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site)
            ? site
            : null;

        if (_isExpeditionDrafting)
        {
            ClampExpeditionDraftCounts();
            string sourceName = ResolveSiteDisplayName(_expeditionSourceSiteId);
            AddMutedLine(_actionList, $"出发场域：{sourceName}");
            AddMutedLine(_actionList, $"已选：{BuildExpeditionUnitText()}");

            foreach ((string unitTypeId, int available) in GetAvailableExpeditionUnits(_expeditionSourceSiteId))
            {
                _expeditionUnitCounts.TryGetValue(unitTypeId, out int selected);
                AddExpeditionCountRow(
                    GetUnitLabel(unitTypeId),
                    selected,
                    available,
                    delta => AdjustExpeditionUnitCount(unitTypeId, delta));
            }

            /*
            AddExpeditionCountRow(
                $"英雄：{GetUnitLabel(StrategicWorldIds.UnitPlayerKnight)}",
                _expeditionHeroCount,
                availableHeroes,
                AdjustExpeditionHeroCount);
            AddExpeditionCountRow(
                $"小兵：{GetUnitLabel(StrategicWorldIds.UnitMilitia)}",
                _expeditionMilitiaCount,
                availableMilitia,
                AdjustExpeditionMilitiaCount);
            */
            AddExpeditionTargetButton(HasSelectedExpeditionUnits());
            AddExpeditionCancelButton();
            return true;
        }

        if (selectedSite?.OwnerFactionId == State.PlayerFactionId &&
            selectedSite.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged)
        {
            bool canEnter = CanEnterSelectedSiteDetail(out string enterFailureReason);
            Button enterButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (enterButton == null)
            {
                return false;
            }

            enterButton.Text = canEnter
                ? "进入场域\n查看详细地图"
                : $"进入场域\n{WorldActionResolver.FormatFailureReason(enterFailureReason)}";
            enterButton.Disabled = !canEnter;
            if (canEnter)
            {
                enterButton.Pressed += EnterSelectedSiteDetail;
            }

            _actionList.AddChild(enterButton);

            bool canStart = CanStartExpeditionFromSite(_selectedSiteId, out string failureReason);
            Button expeditionButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (expeditionButton == null)
            {
                return false;
            }

            expeditionButton.Text = canStart
                ? "出征\n选择英雄和小兵"
                : $"出征\n{WorldActionResolver.FormatFailureReason(failureReason)}";
            expeditionButton.Disabled = !canStart;
            if (canStart)
            {
                expeditionButton.Pressed += BeginExpeditionDraft;
            }

            _actionList.AddChild(expeditionButton);
        }

        return false;
    }

    private void AddExpeditionCountRow(
        string label,
        int selected,
        int available,
        System.Action<int> adjust)
    {
        HBoxContainer countRow = GameUiSceneFactory.CreateWorldExpeditionCountRow(nameof(StrategicWorldRoot));
        if (countRow == null)
        {
            return;
        }

        Button minusButton = GameUiSceneFactory.GetRequiredNode<Button>(
            countRow,
            "MinusButton",
            nameof(StrategicWorldRoot));
        Label countLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            countRow,
            "CountLabel",
            nameof(StrategicWorldRoot));
        Button plusButton = GameUiSceneFactory.GetRequiredNode<Button>(
            countRow,
            "PlusButton",
            nameof(StrategicWorldRoot));

        if (minusButton != null)
        {
            minusButton.Disabled = selected <= 0;
            minusButton.Pressed += () => adjust?.Invoke(-1);
        }

        if (countLabel != null)
        {
            countLabel.Text = $"{label} {selected}/{available}";
        }

        if (plusButton != null)
        {
            plusButton.Disabled = selected >= available;
            plusButton.Pressed += () => adjust?.Invoke(1);
        }

        _actionList.AddChild(countRow);
    }

    private void AddExpeditionTargetButton(bool canChooseTarget)
    {
        Button targetButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (targetButton == null)
        {
            return;
        }

        targetButton.Text = _isExpeditionTargeting
            ? "选择目的地中\n右键场域或空地"
            : "选择目的地\n敌方=进攻 己方=进驻 空地=移动";
        targetButton.Disabled = !canChooseTarget;
        targetButton.Pressed += BeginExpeditionTargeting;
        _actionList.AddChild(targetButton);
    }

    private void AddExpeditionCancelButton()
    {
        Button cancelButton = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(StrategicWorldRoot));
        if (cancelButton == null)
        {
            return;
        }

        cancelButton.Text = "取消出征";
        cancelButton.Pressed += CancelExpeditionDraft;
        _actionList.AddChild(cancelButton);
    }

    private static string BuildActionButtonText(WorldActionViewModel action)
    {
        string costs = action.CostLines.Count == 0 ? "无消耗" : string.Join("，", action.CostLines);
        string suffix = action.IsEnabled ? costs : action.DisabledReason;
        return $"{action.DisplayName}\n{suffix}";
    }

    private bool TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity)
    {
        opportunity = null;
        if (string.IsNullOrWhiteSpace(_selectedOpportunityId) || State?.OpportunityStates == null)
        {
            return false;
        }

        if (State.OpportunityStates.TryGetValue(_selectedOpportunityId, out opportunity) &&
            opportunity.Status == WorldOpportunityStatus.Active)
        {
            return true;
        }

        _selectedOpportunityId = "";
        opportunity = null;
        return false;
    }

    private static string BuildOpportunityRewardText(WorldOpportunityDefinition definition)
    {
        if (definition == null || definition.CompletionRewards.Count == 0)
        {
            return "无固定奖励";
        }

        string[] rewards = definition.CompletionRewards
            .Where(reward => reward.Amount != 0 && !string.IsNullOrWhiteSpace(reward.ResourceId))
            .Select(reward => $"{GetResourceLabel(reward.ResourceId)} {(reward.Amount > 0 ? "+" : "")}{reward.Amount}")
            .ToArray();
        return rewards.Length == 0 ? "无固定奖励" : string.Join("，", rewards);
    }

    private static string GetOpportunityStatusLabel(WorldOpportunityStatus status)
    {
        return status switch
        {
            WorldOpportunityStatus.Active => "可处理",
            WorldOpportunityStatus.Completed => "已完成",
            WorldOpportunityStatus.Expired => "已消失",
            _ => status.ToString()
        };
    }

    private static string GetResourceLabel(string resourceId)
    {
        return resourceId switch
        {
            StrategicWorldIds.ResourcePopulation => "人口",
            StrategicWorldIds.ResourceEconomy => "经济",
            StrategicWorldIds.ResourceStone => "石材",
            _ => resourceId
        };
    }

    private void EnterSelectedSiteDetail()
    {
        if (!CanEnterSelectedSiteDetail(out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return;
        }

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        StrategicWorldRuntime.BeginSiteVisit(_selectedSiteId, returnScenePath);
        StrategicWorldRuntime.LastNotice = $"进入{ResolveSiteDisplayName(_selectedSiteId)}。";
        _worldClockPaused = true;

        Error error = GetTree().ChangeSceneToFile(SiteScenePath);
        if (error == Error.Ok)
        {
            return;
        }

        StrategicWorldRuntime.ClearPendingSiteVisit();
        StrategicWorldRuntime.LastNotice = "无法进入场域。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"Cannot enter site detail site={_selectedSiteId} path={SiteScenePath} error={error}");
        RefreshAll();
    }

    private bool CanEnterSelectedSiteDetail(out string failureReason)
    {
        failureReason = "";
        if (HasAttackingThreat())
        {
            failureReason = "attacking_threat_pending";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_selectedSiteId) ||
            !State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site))
        {
            failureReason = "missing_site";
            return false;
        }

        if (site.OwnerFactionId != State.PlayerFactionId ||
            site.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
        {
            failureReason = "site_not_owned";
            return false;
        }

        return true;
    }

    private void ExecuteAction(WorldActionViewModel viewModel)
    {
        WorldActionRequest request = new()
        {
            ActionId = viewModel.ActionId,
            ActorFactionId = State.PlayerFactionId,
            SourceSiteId = _selectedSiteId,
            TargetSiteId = viewModel.TargetSiteId,
            TargetSlotId = viewModel.TargetSlotId,
            ThreatId = viewModel.ThreatId
        };

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        WorldActionResult result = _actionResolver.Apply(State, Definition, request, returnScenePath, SiteScenePath);
        StrategicWorldRuntime.LastNotice = result.Message;

        if (!result.Success)
        {
            RefreshAll();
            return;
        }

        _worldClockAccumulator = 0.0;
        if (HasAttackingThreat())
        {
            _worldClockPaused = true;
        }

        if (result.BattleStartRequest != null)
        {
            TryEnterBattle(result.BattleStartRequest);
            return;
        }

        RefreshAll();
    }

    private bool TryEnterBattleForArrivedArmy(string armyId)
    {
        if (string.IsNullOrWhiteSpace(armyId) ||
            !State.ArmyStates.TryGetValue(armyId, out WorldArmyState army) ||
            army.OwnerFactionId != State.PlayerFactionId ||
            army.Intent != WorldArmyIntent.AssaultSite)
        {
            return false;
        }

        _worldClockPaused = true;
        _selectedSiteId = army.TargetSiteId;
        _selectedThreatId = "";
        if (!CanBuildAssaultBattleForSite(army.TargetSiteId))
        {
            army.Status = WorldArmyStatus.Idle;
            army.Intent = WorldArmyIntent.None;
            army.ClearNavigationPath();
            _worldClockPaused = HasAttackingThreat();
            StrategicWorldDefinitionQueries queries = new(Definition);
            WorldSiteDefinition siteDefinition = queries.GetSite(army.TargetSiteId);
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            GameLog.Warn(nameof(StrategicWorldRoot), $"Arrived assault army has no battle builder army={army.ArmyId} target={army.TargetSiteId}");
            RefreshAll();
            return true;
        }

        if (State.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site))
        {
            _siteModeTransitions.EnterWartime(site, State.WorldTick, "assault_army_arrived", army.ArmyId);
        }

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildAssaultBonefieldRequest(
            State,
            Definition,
            returnScenePath,
            SiteScenePath,
            army.ArmyId);
        StrategicWorldRuntime.LastNotice = "玩家进攻部队已抵达，进入攻占战。";
        if (!TryEnterBattle(request))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterDefenseRaidBattle(string threatId)
    {
        if (string.IsNullOrWhiteSpace(threatId) ||
            !State.ThreatPlans.TryGetValue(threatId, out EnemyThreatPlan threat) ||
            threat.Stage != ThreatStage.Attacking)
        {
            return false;
        }

        if (!WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threat))
        {
            return false;
        }

        _worldClockPaused = true;
        _selectedThreatId = threat.Id;
        _selectedSiteId = threat.TargetSiteId;

        if (!State.SiteStates.TryGetValue(threat.TargetSiteId, out WorldSiteState site))
        {
            StrategicWorldRuntime.LastNotice = $"敌方部队已抵达，但目标场域不存在：{threat.TargetSiteId}。";
            GameLog.Warn(nameof(StrategicWorldRoot), $"Arrived raid has no target site threat={threat.Id} target={threat.TargetSiteId}");
            RefreshAll();
            return true;
        }

        _siteModeTransitions.EnterWartime(site, State.WorldTick, "raid_army_arrived", threat.Id);

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildDefenseRaidRequest(
            State,
            Definition,
            threat.Id,
            returnScenePath,
            SiteScenePath);
        StrategicWorldRuntime.LastNotice = "敌方部队已抵达，进入守城战。";
        if (!TryEnterBattle(request))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterWorldBattleIntervention(string worldBattleId)
    {
        if (string.IsNullOrWhiteSpace(worldBattleId) ||
            State.WorldBattleStates == null ||
            !State.WorldBattleStates.TryGetValue(worldBattleId, out WorldBattleState battle) ||
            battle.IsResolved)
        {
            StrategicWorldRuntime.LastNotice = "该世界战斗已经结束，无法介入。";
            RefreshAll();
            return false;
        }

        _worldClockPaused = true;
        _selectedThreatId = battle.ThreatId;
        _selectedSiteId = battle.TargetSiteId;
        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildWorldBattleInterventionRequest(
            State,
            Definition,
            battle.BattleId,
            returnScenePath,
            SiteScenePath);
        if (request == null)
        {
            StrategicWorldRuntime.LastNotice = "世界战斗缺少进入战斗所需的上下文。";
            RefreshAll();
            return false;
        }

        StrategicWorldRuntime.LastNotice =
            $"介入世界战斗：{WorldBattleProgressionService.GetPhaseLabel(battle.CurrentPhase)}阶段。";
        if (!TryEnterBattle(request))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterFirstDefenseRaidBattle()
    {
        string threatId = State?.ThreatPlans.Values
            .Where(threat =>
                threat.Stage == ThreatStage.Attacking &&
                WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threat))
            .OrderBy(threat => threat.CreatedTick)
            .Select(threat => threat.Id)
            .FirstOrDefault() ?? "";
        return TryEnterDefenseRaidBattle(threatId);
    }

    private bool TryEnterFieldInterceptBattle(WorldArmyInterceptResult intercept)
    {
        if (intercept == null ||
            string.IsNullOrWhiteSpace(intercept.PlayerArmyId) ||
            string.IsNullOrWhiteSpace(intercept.EnemyArmyId) ||
            !State.ArmyStates.ContainsKey(intercept.PlayerArmyId) ||
            !State.ArmyStates.ContainsKey(intercept.EnemyArmyId))
        {
            return false;
        }

        _worldClockPaused = true;
        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildFieldInterceptRequest(
            State,
            Definition,
            intercept.PlayerArmyId,
            intercept.EnemyArmyId,
            returnScenePath,
            SiteScenePath);
        _selectedSiteId = string.IsNullOrWhiteSpace(request.TargetSiteId)
            ? _selectedSiteId
            : request.TargetSiteId;
        _selectedThreatId = request.ThreatId ?? "";
        StrategicWorldRuntime.LastNotice = "玩家部队与敌军接触，进入野外遭遇战。";
        if (!TryEnterBattle(request))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterBattle(BattleStartRequest request)
    {
        if (request == null)
        {
            return false;
        }

        BeginBattleAnnouncement(request);
        return true;
    }

    private static bool CanBuildAssaultBattleForSite(string siteId)
    {
        return siteId == StrategicWorldIds.SiteBonefield;
    }

    private static string BuildUnsupportedAssaultNotice(WorldSiteDefinition siteDefinition)
    {
        string siteName = string.IsNullOrWhiteSpace(siteDefinition?.DisplayName)
            ? "目标场域"
            : siteDefinition.DisplayName;
        return $"{siteName}暂未配置攻占战，不能进攻。";
    }

    private void RecoverUnsupportedPlayerAssaultArmies()
    {
        if (State?.ArmyStates == null)
        {
            return;
        }

        int recovered = 0;
        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.OwnerFactionId != State.PlayerFactionId ||
                army.Status != WorldArmyStatus.Attacking ||
                army.Intent != WorldArmyIntent.AssaultSite ||
                CanBuildAssaultBattleForSite(army.TargetSiteId))
            {
                continue;
            }

            army.Status = WorldArmyStatus.Idle;
            army.Intent = WorldArmyIntent.None;
            army.ClearNavigationPath();
            recovered++;
        }

        if (recovered > 0)
        {
            GameLog.Warn(nameof(StrategicWorldRoot), $"RecoveredUnsupportedPlayerAssaultArmies count={recovered}");
        }
    }

    private void BeginBattleAnnouncement(BattleStartRequest request)
    {
        _pendingBattleRequest = request;
        _worldClockPaused = true;
        Vector2 focusPosition = ResolveBattleFocusMapPosition(request);
        FocusWorldMapOn(focusPosition);
        StrategicWorldRuntime.LastNotice = "发生了战斗。";
        RefreshAll();
        ShowBattleAlertDialog();
        GameLog.Info(nameof(StrategicWorldRoot), $"BattleAnnouncement request={request.RequestId} kind={request.BattleKind} focus={focusPosition}");
    }

    private void ShowBattleAlertDialog()
    {
        EnsureBattleAlertDialog();
        if (_battleAlertDialog == null)
        {
            return;
        }

        _battleAlertDialog.DialogText = "发生了战斗。";
        _activeBattleGateDialog = "alert";
        _battleAlertDialog.PopupCentered(new Vector2I(360, 150));
    }

    private void EnsureBattleAlertDialog()
    {
        if (_battleAlertDialog != null)
        {
            return;
        }

        _battleAlertDialog = GameUiSceneFactory.Instantiate<AcceptDialog>(
            GameUiSceneFactory.BattleAlertDialogScenePath,
            nameof(StrategicWorldRoot));
        if (_battleAlertDialog == null)
        {
            return;
        }

        GameUiSkin.ApplyDialog(_battleAlertDialog);
        ConfigureBattleGateDialog(_battleAlertDialog);
        _battleAlertDialog.CloseRequested += OnBattleGateDialogCloseRequested;
        _battleAlertDialog.Confirmed += ShowPreBattleDialog;
        AddChild(_battleAlertDialog);
    }

    private void ShowPreBattleDialog()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        EnsurePreBattleDialog();
        if (_preBattleDialog == null)
        {
            return;
        }

        _preBattleDialog.Title = "战前情报";
        _preBattleDialog.DialogText = BuildPreBattleText(_pendingBattleRequest);
        _activeBattleGateDialog = "prebattle";
        _preBattleDialog.PopupCentered(new Vector2I(560, 460));
    }

    private void EnsurePreBattleDialog()
    {
        if (_preBattleDialog != null)
        {
            return;
        }

        _preBattleDialog = GameUiSceneFactory.Instantiate<AcceptDialog>(
            GameUiSceneFactory.PreBattleDialogScenePath,
            nameof(StrategicWorldRoot));
        if (_preBattleDialog == null)
        {
            return;
        }

        GameUiSkin.ApplyDialog(_preBattleDialog);
        ConfigureBattleGateDialog(_preBattleDialog);
        _preBattleDialog.CloseRequested += OnBattleGateDialogCloseRequested;
        _preBattleDialog.Confirmed += LaunchPendingBattle;
        AddChild(_preBattleDialog);
    }

    private static void ConfigureBattleGateDialog(AcceptDialog dialog)
    {
        if (dialog == null)
        {
            return;
        }

        dialog.Exclusive = true;
        dialog.Unresizable = true;
        dialog.Borderless = true;
    }

    private void OnBattleGateDialogCloseRequested()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenActiveBattleGateDialog));
    }

    private void ReopenActiveBattleGateDialog()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        if (_activeBattleGateDialog == "prebattle")
        {
            ShowPreBattleDialog();
            return;
        }

        ShowBattleAlertDialog();
    }

    private void LaunchPendingBattle()
    {
        BattleStartRequest request = _pendingBattleRequest;
        _pendingBattleRequest = null;
        _activeBattleGateDialog = "";
        if (request == null)
        {
            return;
        }

        BattleSessionHandoff.BeginBattle(request);
        Error error = GetTree().ChangeSceneToFile(request.SiteScenePath);
        if (error == Error.Ok)
        {
            return;
        }

        BattleSessionHandoff.CancelBattle();
        StrategicWorldRuntime.LastNotice = "无法进入战棋战斗。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"Cannot enter site path={request.SiteScenePath} error={error}");
        RefreshAll();
    }

    private string BuildPreBattleText(BattleStartRequest request)
    {
        List<string> lines = new()
        {
            GetBattleKindLabel(request.BattleKind),
            "",
            "双方部队",
            $"我方：{BuildForceSummary(request.PlayerForces, request.SourceArmyId, true)}",
            $"敌方：{BuildForceSummary(request.EnemyForces, request.TargetArmyId, false)}"
        };

        if (request.BattleKind is BattleKind.AssaultSite or BattleKind.DefenseRaid)
        {
            lines.Add("");
            lines.Add("城池信息");
            lines.Add(BuildSiteBattleSummary(request));
        }

        if (!string.IsNullOrWhiteSpace(request.WorldBattleId) &&
            State.WorldBattleStates != null &&
            State.WorldBattleStates.TryGetValue(request.WorldBattleId, out WorldBattleState worldBattle))
        {
            lines.Add("");
            lines.Add("世界战斗");
            lines.Add($"阶段：{WorldBattleProgressionService.GetPhaseLabel(worldBattle.CurrentPhase)}");
            lines.Add($"自动推演：{WorldBattleProgressionService.GetOutcomeLabel(worldBattle.ProjectedOutcome)}");
            lines.Add($"剩余：{WorldBattleProgressionService.GetRemainingTicks(State, worldBattle)} 个世界步");
        }

        return string.Join("\n", lines);
    }

    private string BuildForceSummary(IReadOnlyCollection<BattleForceRequest> forces, string armyId, bool playerSide)
    {
        List<string> unitLines = forces?
            .Where(force => force.Count > 0)
            .Select(force => $"{GetUnitLabel(force.UnitDefinitionId)} x{force.Count}")
            .ToList() ?? new List<string>();

        if (unitLines.Count == 0 &&
            !string.IsNullOrWhiteSpace(armyId) &&
            State.ArmyStates.TryGetValue(armyId, out WorldArmyState army))
        {
            unitLines.AddRange(army.GarrisonUnits
                .Where(unit => unit.Count > 0)
                .Select(unit => $"{GetUnitLabel(unit.UnitTypeId)} x{unit.Count}"));

            if (unitLines.Count == 0)
            {
                return $"{GetFactionLabel(army.OwnerFactionId)}部队（未配置单位明细）";
            }
        }

        if (unitLines.Count == 0 &&
            !playerSide &&
            _pendingBattleRequest?.BattleKind == BattleKind.AssaultSite &&
            !string.IsNullOrWhiteSpace(_pendingBattleRequest.TargetSiteId) &&
            State.SiteStates.TryGetValue(_pendingBattleRequest.TargetSiteId, out WorldSiteState targetSite))
        {
            unitLines.AddRange(targetSite.Garrison
                .Where(unit => unit.Count > 0)
                .Select(unit => $"{GetUnitLabel(unit.UnitTypeId)} x{unit.Count}"));
        }

        if (unitLines.Count == 0 &&
            !playerSide &&
            !string.IsNullOrWhiteSpace(_pendingBattleRequest?.ThreatId) &&
            State.ThreatPlans.TryGetValue(_pendingBattleRequest.ThreatId, out EnemyThreatPlan threat))
        {
            return $"敌军编组 {threat.EnemyGroupId}";
        }

        return unitLines.Count == 0 ? "未配置部队详情" : string.Join("，", unitLines);
    }

    private string BuildSiteBattleSummary(BattleStartRequest request)
    {
        string siteId = !string.IsNullOrWhiteSpace(request.TargetSiteId)
            ? request.TargetSiteId
            : request.SiteStateSnapshot?.SiteId ?? "";
        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition definition = queries.GetSite(siteId);
        WorldSiteState site = !string.IsNullOrWhiteSpace(siteId) && State.SiteStates.TryGetValue(siteId, out WorldSiteState value)
            ? value
            : null;

        if (site == null)
        {
            return "未找到城池状态。";
        }

        string garrison = site.Garrison.Count == 0
            ? "无"
            : string.Join("，", site.Garrison.Where(item => item.Count > 0).Select(item => $"{GetUnitLabel(item.UnitTypeId)} x{item.Count}"));
        int activeFacilities = site.Facilities.Count(item => item.State == FacilityState.Active);
        int damagedFacilities = site.Facilities.Count(item => item.State == FacilityState.Damaged);

        return
            $"场域：{definition?.DisplayName ?? site.SiteId}\n" +
            $"状态：{GetControlStateLabel(site.ControlState)}\n" +
            $"归属：{GetFactionLabel(site.OwnerFactionId)}\n" +
            $"受损：{site.DamageLevel}\n" +
            $"建筑：运行 {activeFacilities}，受损 {damagedFacilities}\n" +
            $"驻军：{garrison}";
    }

    private Vector2 ResolveBattleFocusMapPosition(BattleStartRequest request)
    {
        if (request.BattleKind == BattleKind.FieldIntercept &&
            State.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState playerArmy) &&
            State.ArmyStates.TryGetValue(request.TargetArmyId, out WorldArmyState enemyArmy))
        {
            return (playerArmy.WorldPosition + enemyArmy.WorldPosition) / 2.0f;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceArmyId) &&
            State.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState sourceArmy))
        {
            return sourceArmy.WorldPosition;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetArmyId) &&
            State.ArmyStates.TryGetValue(request.TargetArmyId, out WorldArmyState targetArmy))
        {
            return targetArmy.WorldPosition;
        }

        if (!string.IsNullOrWhiteSpace(request.ThreatId) &&
            State.ThreatPlans.TryGetValue(request.ThreatId, out EnemyThreatPlan threat) &&
            !string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
            State.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState threatArmy))
        {
            return threatArmy.WorldPosition;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition targetSite = queries.GetSite(request.TargetSiteId);
        if (targetSite != null)
        {
            return targetSite.MapPosition;
        }

        WorldSiteDefinition sourceSite = queries.GetSite(request.SourceSiteId);
        return sourceSite?.MapPosition ?? Vector2.Zero;
    }

    private void FocusWorldMapOn(Vector2 mapPosition)
    {
        if (_worldCamera == null || !float.IsFinite(mapPosition.X) || !float.IsFinite(mapPosition.Y))
        {
            return;
        }

        _worldCamera.FocusOn(mapPosition);
        UpdateWorldCameraView(true);
    }

    private void SaveWorld()
    {
        _saveService.Save(State, out string message);
        StrategicWorldRuntime.LastNotice = message;
        RefreshAll();
        TryEnterFirstDefenseRaidBattle();
    }

    private void LoadWorld()
    {
        if (_saveService.TryLoad(out StrategicWorldState state, out string message))
        {
            StrategicWorldRuntime.ReplaceState(state);
            _selectedSiteId = Definition.StartingSiteId;
            _selectedThreatId = "";
            _selectedOpportunityId = "";
            EnsureWorldBattlesForAttackingThreats();
            _worldClockPaused = HasAttackingThreat();
            _worldClockAccumulator = 0.0;
        }

        StrategicWorldRuntime.LastNotice = message;
        RefreshAll();
    }

    private void ResetWorld()
    {
        StrategicWorldRuntime.Reset();
        SyncDefinitionMapPositionsFromAnchors();
        RebuildSiteVisualFootprints();
        _selectedSiteId = Definition.StartingSiteId;
        _selectedThreatId = "";
        _selectedOpportunityId = "";
        _worldClockPaused = false;
        _worldClockAccumulator = 0.0;
        RefreshAll();
    }

    private void ResolveWorldMapNodes()
    {
        _worldMapRoot = GetNodeOrNull<Node2D>(WorldMapRootPath);
        if (_worldMapRoot == null)
        {
            _worldMapRoot = new Node2D
            {
                Name = "WorldMapRoot",
                ZIndex = -20
            };
            AddChild(_worldMapRoot);
        }
        else
        {
            _worldMapRoot.ZIndex = System.Math.Min(_worldMapRoot.ZIndex, -20);
        }

        Node2D mapAnchors = GetOrCreateNode2D(_worldMapRoot, "MapAnchors");
        _siteAnchorRoot = GetNodeOrNull<Node2D>(SiteAnchorRootPath) ?? GetOrCreateNode2D(mapAnchors, "Sites");
        _siteVisualLayer = GetNodeOrNull<TileMapLayer>(SiteVisualLayerPath) ?? _worldMapRoot.GetNodeOrNull<TileMapLayer>("SiteVisualLayer");
        _armySpawnPointRoot = GetNodeOrNull<Node2D>(ArmySpawnPointRootPath) ?? GetOrCreateNode2D(mapAnchors, "ArmySpawnPoints");
        _ = GetNodeOrNull<Node2D>(EncounterZoneRootPath) ?? GetOrCreateNode2D(mapAnchors, "EncounterZones");
    }

    private void ResolveWorldCamera()
    {
        _worldCamera = GetNodeOrNull<MapCameraController>(WorldCameraPath);
        if (_worldCamera == null)
        {
            _worldCamera = new MapCameraController
            {
                Name = "WorldCamera",
                UseViewportCamera = false,
                MinZoom = 0.5f,
                MaxZoom = 3.0f,
                Zoom = Vector2.One
            };
            AddChild(_worldCamera);
        }

        _worldCamera.UseViewportCamera = false;
        _worldCamera.Enabled = false;
        _worldCamera.ProcessPriority = -20;
    }

    private void ConfigureWorldCamera()
    {
        if (_worldCamera == null)
        {
            return;
        }

        _worldCamera.SetViewportSizeOverride(GetMapBounds().Size);
        if (TryCalculateStrategicMapBounds(out Rect2 mapBounds))
        {
            _worldCamera.SetMapBounds(mapBounds);
            if (_worldCamera.GlobalPosition == Vector2.Zero)
            {
                _worldCamera.FocusOn(mapBounds.GetCenter());
            }

            GameLog.Info(nameof(StrategicWorldRoot), $"StrategicWorldCameraConfigured bounds={mapBounds}");
            return;
        }

        _worldCamera.ClearMapBounds();
        GameLog.Warn(nameof(StrategicWorldRoot), "StrategicWorldCameraBoundsMissing");
    }

    private bool UpdateWorldCameraView(bool force = false)
    {
        if (_worldCamera == null || _worldMapRoot == null)
        {
            return false;
        }

        Rect2 mapViewBounds = GetMapBounds();
        _worldCamera.SetViewportSizeOverride(mapViewBounds.Size);

        Vector2 zoom = _worldCamera.Zoom;
        _worldMapRoot.GlobalScale = zoom;
        _worldMapRoot.GlobalPosition = mapViewBounds.GetCenter() - _worldCamera.GlobalPosition * zoom;

        bool changed = force ||
                       _lastWorldMapRootPosition.DistanceSquaredTo(_worldMapRoot.GlobalPosition) > 0.001f ||
                       _lastWorldMapRootScale.DistanceSquaredTo(_worldMapRoot.GlobalScale) > 0.0001f;
        if (!changed)
        {
            return false;
        }

        _lastWorldMapRootPosition = _worldMapRoot.GlobalPosition;
        _lastWorldMapRootScale = _worldMapRoot.GlobalScale;
        if (Definition != null && State != null && _siteButtons.Count > 0)
        {
            RefreshSiteButtons(new StrategicWorldDefinitionQueries(Definition));
        }

        QueueRedraw();
        return true;
    }

    private bool TryCalculateStrategicMapBounds(out Rect2 bounds)
    {
        bounds = default;
        bool hasPoint = false;

        foreach (TileMapLayer layer in GetStrategicMapTileLayers())
        {
            foreach (Vector2I cell in layer.GetUsedCells())
            {
                foreach (Vector2 point in BuildTileCellMapPolygon(layer, cell))
                {
                    ExpandBounds(point, ref bounds, ref hasPoint);
                }
            }
        }

        if (_siteVisualLayer != null)
        {
            foreach (Vector2I cell in _siteVisualLayer.GetUsedCells())
            {
                foreach (Vector2 point in BuildTileCellMapPolygon(_siteVisualLayer, cell))
                {
                    ExpandBounds(point, ref bounds, ref hasPoint);
                }
            }
        }

        if (_siteAnchorRoot != null)
        {
            foreach (Node child in _siteAnchorRoot.GetChildren())
            {
                if (child is Node2D anchor)
                {
                    ExpandBounds(_worldMapRoot.ToLocal(anchor.GlobalPosition), ref bounds, ref hasPoint);
                }
            }
        }

        if (hasPoint)
        {
            bounds = bounds.Grow(96.0f);
        }

        return hasPoint;
    }

    private Vector2[] BuildTileCellMapPolygon(TileMapLayer layer, Vector2I cell)
    {
        Vector2 center = layer.MapToLocal(cell);
        Vector2 stepX = layer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = layer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return new[]
        {
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[0])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[1])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[2])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[3]))
        };
    }

    private IEnumerable<TileMapLayer> GetStrategicMapTileLayers()
    {
        if (_worldMapRoot == null)
        {
            yield break;
        }

        foreach (Node child in _worldMapRoot.GetChildren())
        {
            if (child is TileMapLayer tileMapLayer)
            {
                yield return tileMapLayer;
            }
        }
    }

    private static void ExpandBounds(Vector2 point, ref Rect2 bounds, ref bool hasPoint)
    {
        if (!hasPoint)
        {
            bounds = new Rect2(point, Vector2.Zero);
            hasPoint = true;
            return;
        }

        bounds = bounds.Expand(point);
    }

    private void RebuildSiteVisualFootprints()
    {
        _siteVisualFootprints.Clear();
        if (Definition == null || _worldMapRoot == null)
        {
            return;
        }

        if (_siteVisualLayer == null)
        {
            ReportSiteVisualFootprintFailure("layer", "missing_site_visual_layer");
            return;
        }

        HashSet<Vector2I> usedCells = new();
        foreach (Vector2I cell in _siteVisualLayer.GetUsedCells())
        {
            usedCells.Add(cell);
        }

        if (usedCells.Count == 0)
        {
            ReportSiteVisualFootprintFailure("layer", "empty_site_visual_layer");
            return;
        }

        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            if (TryBuildSiteVisualFootprint(site, usedCells, out SiteVisualFootprint footprint, out string failureReason))
            {
                _siteVisualFootprints[site.Id] = footprint;
                continue;
            }

            ReportSiteVisualFootprintFailure(site?.Id ?? "", failureReason);
        }

        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteVisualFootprintsBuilt count={_siteVisualFootprints.Count} layer={_siteVisualLayer.Name}");
    }

    private bool TryBuildSiteVisualFootprint(
        WorldSiteDefinition site,
        HashSet<Vector2I> usedCells,
        out SiteVisualFootprint footprint,
        out string failureReason)
    {
        footprint = null;
        failureReason = "";
        if (site == null)
        {
            failureReason = "missing_site_definition";
            return false;
        }

        Vector2 mapPosition = GetSiteMapPosition(site);
        Vector2 layerLocalPosition = _siteVisualLayer.ToLocal(_worldMapRoot.ToGlobal(mapPosition));
        Vector2I startCell = _siteVisualLayer.LocalToMap(layerLocalPosition);
        if (!usedCells.Contains(startCell) || _siteVisualLayer.GetCellSourceId(startCell) < 0)
        {
            failureReason = $"anchor_cell_empty cell={startCell}";
            return false;
        }

        HashSet<Vector2I> cells = new();
        Queue<Vector2I> queue = new();
        cells.Add(startCell);
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            foreach (Vector2I direction in SiteVisualScanDirections)
            {
                Vector2I next = current + direction;
                if (!usedCells.Contains(next) ||
                    _siteVisualLayer.GetCellSourceId(next) < 0 ||
                    !cells.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        Rect2 mapBounds = default;
        bool hasPoint = false;
        foreach (Vector2I cell in cells)
        {
            foreach (Vector2 point in BuildTileCellMapPolygon(_siteVisualLayer, cell))
            {
                ExpandBounds(point, ref mapBounds, ref hasPoint);
            }
        }

        if (!hasPoint)
        {
            failureReason = "empty_footprint_bounds";
            return false;
        }

        footprint = new SiteVisualFootprint(site.Id, cells, mapBounds);
        return true;
    }

    private void ReportSiteVisualFootprintFailure(string siteId, string reason)
    {
        string key = $"{siteId}:{reason}";
        if (!_reportedSiteVisualFootprintFailures.Add(key))
        {
            return;
        }

        GameLog.Warn(nameof(StrategicWorldRoot), $"SiteVisualFootprintMissing site={siteId} reason={reason}");
    }

    private void ConfigureStrategicNavigationContext()
    {
        if (_worldMapRoot?.GetWorld2D() == null)
        {
            _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable("world2d_missing");
            GameLog.Error(nameof(StrategicWorldRoot), "StrategicNavigationUnavailable reason=world2d_missing");
            return;
        }

        TileMapLayer navigationTileLayer = _worldMapRoot.GetNodeOrNull<TileMapLayer>(StrategicNavigationTileLayerName);
        int navigationCellCount = navigationTileLayer?.GetUsedCells().Count ?? 0;
        if (navigationTileLayer == null || navigationCellCount == 0)
        {
            string reason = navigationTileLayer == null
                ? "strategic_navigation_tile_layer_missing"
                : "strategic_navigation_tile_layer_empty";
            _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable(reason);
            GameLog.Error(nameof(StrategicWorldRoot), $"StrategicNavigationUnavailable reason={reason}");
            return;
        }

        _strategicNavigationContext = StrategicNavigationContext.CreateGodotNavigation(
            _worldMapRoot.GetWorld2D().NavigationMap,
            navigationTileLayer,
            _worldMapRoot);
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"StrategicNavigationConfigured provider={_strategicNavigationContext.PrimaryProviderId} version={_strategicNavigationContext.Version} layer={StrategicNavigationTileLayerName} cells={navigationCellCount}");
    }

    private void SyncDefinitionMapPositionsFromAnchors()
    {
        if (Definition == null || _siteAnchorRoot == null)
        {
            return;
        }

        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            if (_siteAnchorRoot.GetNodeOrNull<Node2D>(site.Id) is not { } anchor)
            {
                continue;
            }

            Vector2 anchorPosition = _worldMapRoot.ToLocal(anchor.GlobalPosition);
            if (site.MapPosition.DistanceSquaredTo(anchorPosition) <= 0.001f)
            {
                continue;
            }

            site.MapPosition = anchorPosition;
            GameLog.Info(nameof(StrategicWorldRoot), $"StrategicSiteAnchorSynced site={site.Id} position={anchorPosition}");
        }
    }

    private static Node2D GetOrCreateNode2D(Node parent, string name)
    {
        Node2D node = parent.GetNodeOrNull<Node2D>(name);
        if (node != null)
        {
            return node;
        }

        node = new Node2D { Name = name };
        parent.AddChild(node);
        return node;
    }

    private bool EnsureWorldBattlesForAttackingThreats()
    {
        if (State == null || Definition == null)
        {
            return false;
        }

        WorldTickResult result = new() { WorldTick = State.WorldTick };
        _worldBattleProgressionService.EnsureBattlesForAttackingThreats(State, Definition, result);
        if (result.StartedWorldBattleIds.Count == 0)
        {
            return false;
        }

        StrategicWorldRuntime.LastNotice = string.Join("\n", result.Messages);
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"WorldBattlesEnsured started={string.Join(",", result.StartedWorldBattleIds)} tick={State.WorldTick}");
        return true;
    }

    private void UpdateWorldClock(double delta)
    {
        if (!AutoWorldClockEnabled || Definition == null || State == null)
        {
            return;
        }

        if (HasAttackingThreat())
        {
            if (_pendingBattleRequest == null && TryEnterFirstDefenseRaidBattle())
            {
                return;
            }

            _worldClockPaused = true;
            _worldClockAccumulator = 0.0;
            RefreshWorldClockLabel();
            return;
        }

        if (_worldClockPaused)
        {
            RefreshWorldClockLabel();
            return;
        }

        double interval = System.Math.Max(1.0, WorldTickIntervalSeconds);
        _worldClockAccumulator += delta * WorldClockSpeedMultipliers[_worldClockSpeedIndex];
        if (_worldClockAccumulator < interval)
        {
            RefreshWorldClockLabel();
            return;
        }

        _worldClockAccumulator %= interval;
        AdvanceWorldClockTick();
    }

    private void AdvanceWorldClockTick()
    {
        WorldTickResult tickResult = _worldTickService.AdvanceWorldTick(State, Definition);
        List<string> messages = new() { $"世界推进到 {tickResult.WorldTick}。" };
        messages.AddRange(tickResult.Messages);

        if (tickResult.AttackingThreatIds.Count > 0)
        {
            _selectedThreatId = tickResult.AttackingThreatIds[0];
            if (State.ThreatPlans.TryGetValue(_selectedThreatId, out EnemyThreatPlan threat))
            {
                _selectedSiteId = threat.TargetSiteId;
                messages.Add("敌方已抵达，世界战斗开始推演。");
            }

            string playerThreatId = tickResult.AttackingThreatIds
                .FirstOrDefault(threatId => WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threatId)) ?? "";
            if (!string.IsNullOrWhiteSpace(playerThreatId))
            {
                StrategicWorldRuntime.LastNotice = string.Join("\n", messages);
                if (TryEnterDefenseRaidBattle(playerThreatId))
                {
                    GameLog.Info(nameof(StrategicWorldRoot), $"WorldClockTick tick={State.WorldTick} immediatePlayerBattle={playerThreatId}");
                    return;
                }
            }

            if (HasAttackingThreat())
            {
                _worldClockPaused = true;
                messages.Add("敌方已抵达，但世界战斗未能创建，世界时钟已暂停。");
            }
        }

        StrategicWorldRuntime.LastNotice = string.Join("\n", messages);
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldClockTick tick={State.WorldTick} paused={_worldClockPaused}");
        RefreshAll();
    }

    private void UpdateWorldArmyMovement(double delta)
    {
        if (!AutoWorldClockEnabled || Definition == null || State == null || _worldClockPaused || HasAttackingThreat())
        {
            return;
        }

        RecoverStalledMarchingThreatArmies();
        if (!State.ArmyStates.Values.Any(army => army.Status == WorldArmyStatus.Moving))
        {
            return;
        }

        ResolveMovingArmySiteNavigationPoints();
        WorldArmyMovementResult result = _armyMovementService.AdvanceArmies(
            State,
            Definition,
            delta * WorldClockSpeedMultipliers[_worldClockSpeedIndex],
            _strategicNavigationContext);
        if (result.ArrivedArmyIds.Count > 0 || result.Messages.Count > 0)
        {
            if (result.AttackingThreatIds.Count > 0)
            {
                _selectedThreatId = result.AttackingThreatIds[0];
                if (State.ThreatPlans.TryGetValue(_selectedThreatId, out EnemyThreatPlan threat))
                {
                    _selectedSiteId = threat.TargetSiteId;
                }

                string playerThreatId = result.AttackingThreatIds
                    .FirstOrDefault(threatId => WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threatId)) ?? "";
                if (!string.IsNullOrWhiteSpace(playerThreatId) &&
                    TryEnterDefenseRaidBattle(playerThreatId))
                {
                    return;
                }

                WorldTickResult battleStartResult = new() { WorldTick = State.WorldTick };
                _worldBattleProgressionService.EnsureBattlesForAttackingThreats(State, Definition, battleStartResult);
                result.Events.AddRange(battleStartResult.Events);
                result.Messages.AddRange(battleStartResult.Messages);
                if (HasAttackingThreat())
                {
                    _worldClockPaused = true;
                    result.Messages.Add("敌方已抵达，但世界战斗未能创建，世界时钟已暂停。");
                }
            }

            if (result.BattleReadyArmyIds.Count > 0 &&
                TryEnterBattleForArrivedArmy(result.BattleReadyArmyIds[0]))
            {
                return;
            }

            if (result.FieldIntercepts.Count > 0 &&
                TryEnterFieldInterceptBattle(result.FieldIntercepts[0]))
            {
                return;
            }

            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0
                ? string.Join("\n", result.Messages)
                : $"部队已抵达目标：{string.Join("，", result.ArrivedArmyIds)}。";
            RefreshAll();
            return;
        }

        QueueRedraw();
    }

    private void RecoverStalledMarchingThreatArmies()
    {
        if (State == null || Definition == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        foreach (EnemyThreatPlan threat in State.ThreatPlans.Values)
        {
            if (threat.Stage != ThreatStage.Marching ||
                string.IsNullOrWhiteSpace(threat.WorldArmyId) ||
                !State.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army) ||
                army.Status != WorldArmyStatus.Idle ||
                !_recoveredStalledThreatArmies.Add(army.ArmyId))
            {
                continue;
            }

            WorldSiteDefinition targetSite = queries.GetSite(threat.TargetSiteId);
            army.Status = WorldArmyStatus.Moving;
            army.Intent = WorldArmyIntent.Raid;
            army.SourceSiteId = threat.SourceSiteId;
            army.TargetSiteId = threat.TargetSiteId;
            army.RelatedThreatId = threat.Id;
            if (targetSite != null)
            {
                army.Destination = GetSiteMapPosition(targetSite);
            }

            army.ClearNavigationPath();
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
            GameLog.Warn(
                nameof(StrategicWorldRoot),
                $"ThreatArmyRecovered threat={threat.Id} army={army.ArmyId} source={army.SourceSiteId} target={army.TargetSiteId} position={army.WorldPosition} destination={army.Destination}");
        }
    }

    private void ToggleWorldClock()
    {
        if (HasAttackingThreat())
        {
            _worldClockPaused = true;
            StrategicWorldRuntime.LastNotice = "敌方正在进攻，必须先处理威胁。";
        }
        else
        {
            _worldClockPaused = !_worldClockPaused;
            StrategicWorldRuntime.LastNotice = _worldClockPaused ? "世界时钟已暂停。" : "世界时钟继续推进。";
        }

        RefreshAll();
    }

    private void CycleWorldClockSpeed()
    {
        _worldClockSpeedIndex = (_worldClockSpeedIndex + 1) % WorldClockSpeedMultipliers.Length;
        StrategicWorldRuntime.LastNotice = $"世界时钟速度 {WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x。";
        RefreshAll();
    }

    private bool HasAttackingThreat()
    {
        return State?.ThreatPlans.Values.Any(threat =>
            threat.Stage == ThreatStage.Attacking &&
            WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threat) &&
            !WorldBattleProgressionService.HasActiveBattleForThreat(State, threat.Id)) == true;
    }

    private void RefreshWorldClockLabel()
    {
        if (_worldClockLabel == null)
        {
            return;
        }

        double interval = System.Math.Max(1.0, WorldTickIntervalSeconds);
        double remaining = _worldClockPaused ? interval : System.Math.Max(0.0, interval - _worldClockAccumulator);
        string status = !AutoWorldClockEnabled
            ? "关闭"
            : _worldClockPaused
                ? "暂停"
                : $"运行 {WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
        _worldClockLabel.Text = $"世界推进：{status}\n下一世界步：{System.Math.Ceiling(remaining):0}s";

        if (_worldClockToggleButton != null)
        {
            _worldClockToggleButton.Text = _worldClockPaused ? "继续" : "暂停";
            _worldClockToggleButton.TooltipText = _worldClockPaused ? "继续世界推进" : "暂停世界推进";
        }

        if (_worldClockSpeedButton != null)
        {
            _worldClockSpeedButton.Text = $"{WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
            _worldClockSpeedButton.TooltipText = $"快进速度：{WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
        }
    }

    private bool HasConfiguredWorldMapSurface()
    {
        if (_worldMapRoot == null)
        {
            return false;
        }

        foreach (Node child in _worldMapRoot.GetChildren())
        {
            string childName = child.Name.ToString();
            if (childName is "MapAnchors")
            {
                continue;
            }

            if (child is TileMapLayer tileMapLayer)
            {
                if (tileMapLayer.GetUsedCells().Count > 0)
                {
                    return true;
                }

                continue;
            }

            return true;
        }

        return false;
    }

    private List<Vector2> GetLegacyThreatNavigationPoints(EnemyThreatPlan threat, Vector2 sourceCenter, Vector2 targetCenter)
    {
        Vector2 sourceMapPosition = ScreenToMap(sourceCenter);
        Vector2 targetMapPosition = ScreenToMap(targetCenter);
        if (_strategicNavigationContext.TryBuildPath(
                sourceMapPosition,
                targetMapPosition,
                out StrategicNavigationPath path,
                out string failureReason))
        {
            return path.Points.Select(MapToScreen).ToList();
        }

        string failureKey = $"{threat?.Id ?? ""}:{failureReason}";
        if (_reportedThreatNavigationFailures.Add(failureKey))
        {
            GameLog.Error(
                nameof(StrategicWorldRoot),
                $"ThreatNavigationPathFailed threat={threat?.Id ?? ""} rule={threat?.RuleId ?? ""} reason={failureReason}");
        }

        return new List<Vector2>();
    }

    private static Vector2 SamplePolyline(IReadOnlyList<Vector2> points, float progress)
    {
        if (points == null || points.Count == 0)
        {
            return Vector2.Zero;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        float totalLength = 0.0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            totalLength += points[i].DistanceTo(points[i + 1]);
        }

        if (totalLength <= 0.001f)
        {
            return points[^1];
        }

        float targetLength = Mathf.Clamp(progress, 0.0f, 1.0f) * totalLength;
        float walked = 0.0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            float segmentLength = points[i].DistanceTo(points[i + 1]);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            if (walked + segmentLength >= targetLength)
            {
                float segmentProgress = (targetLength - walked) / segmentLength;
                return points[i].Lerp(points[i + 1], segmentProgress);
            }

            walked += segmentLength;
        }

        return points[^1];
    }

    private static void AddMutedLine(Container parent, string text)
    {
        Label label = GameUiSceneFactory.CreateWorldMutedLine(nameof(StrategicWorldRoot));
        if (label == null)
        {
            return;
        }

        label.Text = text;
        parent.AddChild(label);
    }

    private static void ClearChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string GetControlStateLabel(SiteControlState state)
    {
        return state switch
        {
            SiteControlState.Unknown => "未知",
            SiteControlState.Neutral => "中立",
            SiteControlState.Hostile => "敌控",
            SiteControlState.Contested => "争夺中",
            SiteControlState.PlayerHeld => "玩家控制",
            SiteControlState.Damaged => "受损",
            SiteControlState.Lost => "丢失",
            _ => "未知"
        };
    }

    private static string GetSiteKindLabel(WorldSiteKind kind)
    {
        return kind switch
        {
            WorldSiteKind.Base => "据点",
            WorldSiteKind.ResourceSite => "资源点",
            WorldSiteKind.EnemySource => "敌方源头",
            _ => "场域"
        };
    }

    private static string GetSiteModeLabel(WorldSiteMode mode)
    {
        return mode switch
        {
            WorldSiteMode.Peacetime => "非战时",
            WorldSiteMode.Alert => "警戒",
            WorldSiteMode.Wartime => "战时",
            WorldSiteMode.Aftermath => "战后",
            _ => "未知"
        };
    }

    private static string GetFactionLabel(string factionId)
    {
        return factionId switch
        {
            StrategicWorldIds.FactionPlayer => "玩家",
            StrategicWorldIds.FactionUndead => "亡灵",
            "" => "无",
            _ => factionId
        };
    }

    private static string GetFacilityStateLabel(FacilityState state)
    {
        return state switch
        {
            FacilityState.Planned => "规划",
            FacilityState.Building => "建造中",
            FacilityState.Active => "运行",
            FacilityState.Damaged => "受损",
            FacilityState.Disabled => "停用",
            FacilityState.Destroyed => "摧毁",
            _ => "未知"
        };
    }

    private static string GetUnitLabel(string unitTypeId)
    {
        return unitTypeId switch
        {
            StrategicWorldIds.UnitMilitia => "民兵",
            StrategicWorldIds.UnitPlayerKnight => "骑士",
            StrategicWorldIds.UnitSkeletonWarrior => "骸骨斥候",
            StrategicWorldIds.UnitSkeletonArcher => "腐骨射手",
            StrategicWorldIds.UnitGraveShadow => "坟场暗影",
            StrategicWorldIds.UnitGraveMarksman => "暗影射手",
            StrategicWorldIds.UnitDeathBlighter => "死亡枯萎者",
            _ => unitTypeId
        };
    }

    private static string GetBattleKindLabel(BattleKind kind)
    {
        return kind switch
        {
            BattleKind.AssaultSite => "攻城战",
            BattleKind.DefenseRaid => "守城战",
            BattleKind.FieldIntercept => "野外遭遇战",
            BattleKind.SearchAndExtract => "搜索撤离",
            BattleKind.Rescue => "救援战",
            BattleKind.Sabotage => "破坏战",
            BattleKind.BossAssault => "首领战",
            _ => "战斗"
        };
    }

    private static Color GetSiteColor(WorldSiteState state)
    {
        return state.ControlState switch
        {
            SiteControlState.PlayerHeld => new Color(0.52f, 0.84f, 0.68f, 1.0f),
            SiteControlState.Damaged => new Color(0.88f, 0.72f, 0.36f, 1.0f),
            SiteControlState.Hostile => new Color(0.88f, 0.38f, 0.34f, 1.0f),
            SiteControlState.Lost => new Color(0.72f, 0.28f, 0.28f, 1.0f),
            SiteControlState.Neutral => new Color(0.66f, 0.72f, 0.78f, 1.0f),
            _ => Colors.White
        };
    }

    private Rect2 GetMapBounds()
    {
        Vector2 viewport = GetViewportRect().Size;
        return new Rect2(
            new Vector2(OuterMargin, TopBarHeight + 24.0f),
            new Vector2(viewport.X - DetailWidth - OuterMargin * 3.0f, viewport.Y - TopBarHeight - OuterMargin * 2.0f));
    }

    private Vector2 MapToScreen(Vector2 mapPosition)
    {
        return _worldMapRoot?.ToGlobal(mapPosition) ?? mapPosition;
    }

    private Vector2 ScreenToMap(Vector2 screenPosition)
    {
        return _worldMapRoot?.ToLocal(screenPosition) ?? screenPosition;
    }

    private static Rect2 BuildScreenRect(Vector2 a, Vector2 b)
    {
        Vector2 position = new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
        Vector2 size = new(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        return new Rect2(position, size);
    }

    private static bool TryIntersectSegments(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d,
        out float segmentRatio,
        out Vector2 point)
    {
        segmentRatio = 0.0f;
        point = default;
        Vector2 r = b - a;
        Vector2 s = d - c;
        float denominator = Cross(r, s);
        if (Mathf.Abs(denominator) <= 0.0001f)
        {
            return false;
        }

        Vector2 delta = c - a;
        float t = Cross(delta, s) / denominator;
        float u = Cross(delta, r) / denominator;
        if (t < 0.0f || t > 1.0f || u < 0.0f || u > 1.0f)
        {
            return false;
        }

        segmentRatio = t;
        point = a + r * t;
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private Rect2 MapRectToScreen(Rect2 mapRect)
    {
        return BuildScreenRect(MapToScreen(mapRect.Position), MapToScreen(mapRect.End));
    }

    private bool TryGetSiteVisualScreenBounds(string siteId, out Rect2 screenBounds)
    {
        screenBounds = default;
        if (string.IsNullOrWhiteSpace(siteId) ||
            !_siteVisualFootprints.TryGetValue(siteId, out SiteVisualFootprint footprint))
        {
            return false;
        }

        screenBounds = MapRectToScreen(footprint.MapBounds);
        return true;
    }

    private Rect2 GetSiteHitRect(WorldSiteDefinition definition)
    {
        if (definition != null &&
            TryGetSiteVisualScreenBounds(definition.Id, out Rect2 screenBounds))
        {
            return screenBounds.Grow(SiteVisualHitPadding);
        }

        Vector2 center = GetSiteCenter(definition);
        return new Rect2(center - SiteButtonSize / 2.0f, SiteButtonSize);
    }

    private Rect2 GetSiteLabelRect(WorldSiteDefinition definition)
    {
        if (definition != null &&
            TryGetSiteVisualScreenBounds(definition.Id, out Rect2 screenBounds))
        {
            float width = Mathf.Max(SiteLabelFallbackSize.X, screenBounds.Size.X + 24.0f);
            return new Rect2(
                new Vector2(screenBounds.GetCenter().X - width / 2.0f, screenBounds.End.Y + SiteVisualLabelGap),
                new Vector2(width, SiteLabelFallbackSize.Y));
        }

        Vector2 center = GetSiteCenter(definition);
        return new Rect2(
            center + new Vector2(-SiteButtonSize.X / 2.0f - 14.0f, SiteButtonSize.Y / 2.0f - 18.0f),
            SiteLabelFallbackSize);
    }

    private Vector2 GetSiteMapPosition(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return Vector2.Zero;
        }

        if (_worldMapRoot != null &&
            _siteAnchorRoot?.GetNodeOrNull<Node2D>(definition.Id) is { } anchor)
        {
            return _worldMapRoot.ToLocal(anchor.GlobalPosition);
        }

        return definition.MapPosition;
    }

    private Vector2 GetSiteCenter(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return Vector2.Zero;
        }

        return MapToScreen(GetSiteMapPosition(definition));
    }

    private static void SetFullRect(Control control)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
    }
}
