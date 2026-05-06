using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.UI.ActionWheel;

public partial class ActionWheel : Control
{
    [ExportGroup("布局")]

    [Export]
    public float StartAngleDegrees { get; set; } = 198f;

    [Export]
    public float EndAngleDegrees { get; set; } = 342f;

    [Export]
    public float WheelCenterXRatio { get; set; } = 0.41f;

    [Export]
    public int CurveDistributionSamples { get; set; } = 128;

    [Export]
    public float DiagonalLift { get; set; } = 21f;

    [ExportGroup("鲸形权重")]

    [Export]
    public float BodyCenterT { get; set; } = 0.34f;

    [Export]
    public float BodyBulge { get; set; } = 0.2f;

    [Export]
    public float TailStartT { get; set; } = 0.62f;

    [Export]
    public float TailOuterScale { get; set; } = 0.58f;

    [Export]
    public float TailInnerScale { get; set; } = 0.78f;

    [Export]
    public float TailDragLength { get; set; } = 270f;

    [Export]
    public float TailInnerDragScale { get; set; } = 0.62f;

    [Export]
    public float TailSlotDragScale { get; set; } = 0.42f;

    [Export]
    public float TailDrop { get; set; } = 16f;

    [Export]
    public float OuterRadiusX { get; set; } = 495f;

    [Export]
    public float OuterRadiusY { get; set; } = 240f;

    [Export]
    public float InnerRadiusX { get; set; } = 219f;

    [Export]
    public float InnerRadiusY { get; set; } = 102f;

    [Export]
    public float SlotRadiusX { get; set; } = 420f;

    [Export]
    public float SlotRadiusY { get; set; } = 177f;

    [Export]
    public Vector2 SlotSize { get; set; } = new(104, 64);

    [ExportGroup("动效")]

    [Export]
    public float LayerTransitionDuration { get; set; } = 0.42f;

    [Export]
    public float LayerTransitionOffsetDegrees { get; set; } = 48f;

    [Export]
    public float LayerTransitionWidthBurst { get; set; } = 0.2f;

    [Export]
    public float LayerTransitionWidthRipple { get; set; } = 0.045f;

    [Export]
    public float LayerTransitionSwapRatio { get; set; } = 0.43f;

    [Export]
    public float LayerTransitionMinScale { get; set; } = 0.12f;

    [Export]
    public float LayerTransitionSlotRevealRatio { get; set; } = 0.72f;

    [Export]
    public float LayerTransitionCoreRadius { get; set; } = 20f;

    private readonly List<ActionWheelSlot> _slots = new();

    private ActionWheelViewModel _viewModel;
    private string _activeLayerId = ActionWheelLayerIds.Primary;
    private string _activeCommandId = "";
    private string _transitionTargetLayerId = "";
    private float _visualRotationDegrees;
    private float _visualAlpha = 1f;
    private float _transitionElapsed;
    private float _transitionFromRotation;
    private float _transitionWidthScale = 1f;
    private float _transitionWheelScale = 1f;
    private float _transitionSlotAlpha = 1f;
    private float _transitionDirection = 1f;
    private bool _transitionLayerSwapped;
    private bool _isTransitioning;

    public event Action<ActionWheelCommandViewModel> CommandHovered;
    public event Action<ActionWheelCommandViewModel> CommandSelected;
    public event Action<ActionWheelCommandViewModel> InvalidCommandSelected;
    public event Action<string> LayerChanged;

    public bool HasActiveCommand => !string.IsNullOrWhiteSpace(_activeCommandId);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!_isTransitioning)
        {
            SetProcess(false);
            return;
        }

        _transitionElapsed += (float)delta;
        float t = Mathf.Clamp(_transitionElapsed / Mathf.Max(LayerTransitionDuration, 0.001f), 0f, 1f);

        if (!_transitionLayerSwapped && t >= LayerTransitionSwapRatio)
        {
            SwitchTransitionLayer();
        }

        _visualRotationDegrees = GetTransitionRotation(t);
        _visualAlpha = GetTransitionWheelAlpha(t);
        _transitionWidthScale = GetTransitionWidthScale(t);
        _transitionWheelScale = GetTransitionWheelScale(t);
        _transitionSlotAlpha = GetTransitionSlotAlpha(t);
        UpdateSlotLayout();
        QueueRedraw();

        if (t >= 1f)
        {
            if (!_transitionLayerSwapped)
            {
                SwitchTransitionLayer();
            }

            _isTransitioning = false;
            _transitionTargetLayerId = "";
            _transitionLayerSwapped = false;
            _visualRotationDegrees = 0f;
            _visualAlpha = 1f;
            _transitionWidthScale = 1f;
            _transitionWheelScale = 1f;
            _transitionSlotAlpha = 1f;
            UpdateSlotLayout();
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateSlotLayout();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        IReadOnlyList<ActionWheelCommandViewModel> commands = GetActiveCommands();
        if (commands.Count == 0)
        {
            return;
        }

        Vector2 center = GetWheelCenter();
        DrawBackgroundArc(center);

        for (int index = 0; index < commands.Count; index++)
        {
            ActionWheelCommandViewModel command = commands[index];
            (float start, float end) = GetSegmentAngles(index, commands.Count);
            start += _visualRotationDegrees;
            end += _visualRotationDegrees;
            Color fill = GetSegmentColor(command);

            fill.A *= _visualAlpha;
            DrawEllipseRingSector(
                center,
                new Vector2(InnerRadiusX, InnerRadiusY),
                new Vector2(OuterRadiusX, OuterRadiusY),
                start,
                end,
                fill);
        }

        DrawTransitionCore(center);
    }

    public void SetViewModel(ActionWheelViewModel viewModel)
    {
        _viewModel = viewModel;
        _activeLayerId = string.IsNullOrWhiteSpace(viewModel.ActiveLayerId)
            ? ActionWheelLayerIds.Primary
            : viewModel.ActiveLayerId;
        _activeCommandId = viewModel.ActiveCommandId ?? "";
        RebuildSlots();
    }

    public void SetActiveCommand(string commandId)
    {
        _activeCommandId = commandId ?? "";

        foreach (ActionWheelSlot slot in _slots)
        {
            slot.SetActive(slot.Command?.Id == _activeCommandId);
        }

        QueueRedraw();
    }

    public bool HandleCancel()
    {
        if (_isTransitioning)
        {
            return HandleTransitionCancel();
        }

        if (_viewModel == null || !_viewModel.Layers.TryGetValue(_activeLayerId, out ActionWheelLayerViewModel layer))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_activeCommandId))
        {
            SetActiveCommand("");
            return true;
        }

        if (string.IsNullOrWhiteSpace(layer.ParentLayerId))
        {
            return false;
        }

        OpenLayer(layer.ParentLayerId, -1f);
        return true;
    }

    private IReadOnlyList<ActionWheelCommandViewModel> GetActiveCommands()
    {
        if (_viewModel == null || !_viewModel.Layers.TryGetValue(_activeLayerId, out ActionWheelLayerViewModel layer))
        {
            return Array.Empty<ActionWheelCommandViewModel>();
        }

        return layer.Commands;
    }

    private void RebuildSlots()
    {
        foreach (ActionWheelSlot slot in _slots)
        {
            RemoveChild(slot);
            slot.QueueFree();
        }

        _slots.Clear();

        foreach (ActionWheelCommandViewModel command in GetActiveCommands())
        {
            ActionWheelSlot slot = GameUiSceneFactory.CreateActionWheelSlot(nameof(ActionWheel));
            if (slot == null)
            {
                continue;
            }

            slot.Size = SlotSize;
            slot.CustomMinimumSize = SlotSize;
            slot.Pressed += OnSlotPressed;
            slot.CancelRequested += OnSlotCancelRequested;
            slot.Hovered += OnSlotHovered;
            AddChild(slot);
            slot.SetCommand(command);
            slot.SetActive(command.Id == _activeCommandId);
            _slots.Add(slot);
        }

        UpdateSlotLayout();
        QueueRedraw();
    }

    private void OnSlotPressed(ActionWheelSlot slot)
    {
        ActionWheelCommandViewModel command = slot.Command;
        if (command == null)
        {
            return;
        }

        if (!command.IsEnabled)
        {
            InvalidCommandSelected?.Invoke(command);
            return;
        }

        if (command.IsBackCommand)
        {
            HandleCancel();
            return;
        }

        if (!string.IsNullOrWhiteSpace(command.TargetLayerId))
        {
            OpenLayer(command.TargetLayerId, 1f);
            return;
        }

        SetActiveCommand(command.Id);
        CommandSelected?.Invoke(command);
    }

    private void OnSlotHovered(ActionWheelSlot slot)
    {
        if (slot.Command != null)
        {
            CommandHovered?.Invoke(slot.Command);
        }
    }

    private void OnSlotCancelRequested(ActionWheelSlot slot)
    {
        HandleCancel();
    }

    private void OpenLayer(string layerId, float direction)
    {
        if (_isTransitioning ||
            _viewModel == null ||
            !_viewModel.Layers.ContainsKey(layerId) ||
            _activeLayerId == layerId)
        {
            return;
        }

        StartLayerTransition(layerId, direction);
    }

    private void StartLayerTransition(string targetLayerId, float direction)
    {
        _transitionElapsed = 0f;
        _transitionDirection = Mathf.Sign(direction) == 0f ? 1f : Mathf.Sign(direction);
        _transitionTargetLayerId = targetLayerId;
        _transitionFromRotation = _transitionDirection * LayerTransitionOffsetDegrees;
        _transitionLayerSwapped = false;
        _visualRotationDegrees = 0f;
        _visualAlpha = 1f;
        _transitionWidthScale = 1f;
        _transitionWheelScale = 1f;
        _transitionSlotAlpha = 0f;
        _isTransitioning = true;
        SetProcess(true);
        UpdateSlotLayout();
        QueueRedraw();
    }

    private void SwitchTransitionLayer()
    {
        if (_transitionLayerSwapped ||
            string.IsNullOrWhiteSpace(_transitionTargetLayerId) ||
            _viewModel == null ||
            !_viewModel.Layers.ContainsKey(_transitionTargetLayerId))
        {
            return;
        }

        _activeLayerId = _transitionTargetLayerId;
        _activeCommandId = "";
        _transitionLayerSwapped = true;
        RebuildSlots();
        LayerChanged?.Invoke(_activeLayerId);
    }

    private bool HandleTransitionCancel()
    {
        if (!_transitionLayerSwapped)
        {
            FinishTransitionAtCurrentLayer();
            return true;
        }

        FinishTransitionAtCurrentLayer();

        if (_viewModel != null &&
            _viewModel.Layers.TryGetValue(_activeLayerId, out ActionWheelLayerViewModel layer) &&
            !string.IsNullOrWhiteSpace(layer.ParentLayerId))
        {
            OpenLayer(layer.ParentLayerId, -_transitionDirection);
        }

        return true;
    }

    private void FinishTransitionAtCurrentLayer()
    {
        _isTransitioning = false;
        _transitionTargetLayerId = "";
        _transitionLayerSwapped = false;
        _visualRotationDegrees = 0f;
        _visualAlpha = 1f;
        _transitionWidthScale = 1f;
        _transitionWheelScale = 1f;
        _transitionSlotAlpha = 1f;
        SetProcess(false);
        UpdateSlotLayout();
        QueueRedraw();
    }

    private void UpdateSlotLayout()
    {
        IReadOnlyList<ActionWheelCommandViewModel> commands = GetActiveCommands();
        if (commands.Count == 0 || _slots.Count == 0)
        {
            return;
        }

        Vector2 center = GetWheelCenter();

        for (int index = 0; index < _slots.Count; index++)
        {
            float angle = Mathf.DegToRad(GetSlotAngle(index, _slots.Count) + _visualRotationDegrees);
            float t = GetSlotT(index, _slots.Count);
            Vector2 slotCenter = BuildWhalePoint(
                center,
                new Vector2(SlotRadiusX, SlotRadiusY),
                angle,
                t,
                false,
                TailSlotDragScale);
            slotCenter += new Vector2(
                Mathf.Sin(t * Mathf.Pi) * 10f,
                Mathf.Lerp(DiagonalLift, -DiagonalLift * 0.35f, t)) * _transitionWheelScale;
            ActionWheelSlot slot = _slots[index];
            float slotAlpha = _isTransitioning ? _transitionSlotAlpha : _visualAlpha;
            slot.Size = SlotSize;
            slot.Position = slotCenter - SlotSize * 0.5f;
            slot.Modulate = new Color(1f, 1f, 1f, slotAlpha);
            slot.Visible = slotAlpha > 0.02f;
        }
    }

    private Vector2 GetWheelCenter()
    {
        return new Vector2(Size.X * WheelCenterXRatio, Size.Y + InnerRadiusY * 0.24f);
    }

    private float GetSlotAngle(int index, int count)
    {
        return GetAngleFromT(GetSlotT(index, count));
    }

    private (float start, float end) GetSegmentAngles(int index, int count)
    {
        if (count <= 1)
        {
            return (StartAngleDegrees, EndAngleDegrees);
        }

        float segmentFraction = 1f / count;
        float paddingFraction = Mathf.Min(segmentFraction * 0.08f, 0.012f);
        float startFraction = Mathf.Clamp(index * segmentFraction + paddingFraction, 0f, 1f);
        float endFraction = Mathf.Clamp((index + 1) * segmentFraction - paddingFraction, 0f, 1f);

        return (
            GetAngleFromT(GetCurveTAtFraction(startFraction)),
            GetAngleFromT(GetCurveTAtFraction(endFraction)));
    }

    private float GetSlotT(int index, int count)
    {
        if (count <= 1)
        {
            return 0.5f;
        }

        return GetCurveTAtFraction((index + 0.5f) / count);
    }

    private float GetCurveTAtFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);

        if (fraction <= 0f)
        {
            return 0f;
        }

        if (fraction >= 1f)
        {
            return 1f;
        }

        int samples = Mathf.Clamp(CurveDistributionSamples, 24, 512);
        Vector2 center = GetWheelCenter();
        float totalLength = 0f;
        Vector2 previous = BuildDistributionPoint(center, 0f);

        for (int sample = 1; sample <= samples; sample++)
        {
            float t = sample / (float)samples;
            Vector2 current = BuildDistributionPoint(center, t);
            totalLength += previous.DistanceTo(current);
            previous = current;
        }

        if (totalLength <= 0.001f)
        {
            return fraction;
        }

        float targetLength = totalLength * fraction;
        float accumulatedLength = 0f;
        previous = BuildDistributionPoint(center, 0f);

        for (int sample = 1; sample <= samples; sample++)
        {
            float t = sample / (float)samples;
            Vector2 current = BuildDistributionPoint(center, t);
            float segmentLength = previous.DistanceTo(current);

            if (accumulatedLength + segmentLength >= targetLength)
            {
                float local = (targetLength - accumulatedLength) / Mathf.Max(segmentLength, 0.001f);
                return Mathf.Lerp((sample - 1) / (float)samples, t, local);
            }

            accumulatedLength += segmentLength;
            previous = current;
        }

        return 1f;
    }

    private Vector2 BuildDistributionPoint(Vector2 center, float t)
    {
        Vector2 radius = new(
            (InnerRadiusX + OuterRadiusX) * 0.5f,
            (InnerRadiusY + OuterRadiusY) * 0.5f);
        float angle = Mathf.DegToRad(GetAngleFromT(t));

        return BuildWhalePoint(center, radius, angle, t, false, 0.74f);
    }

    private float GetAngleFromT(float t)
    {
        return Mathf.Lerp(StartAngleDegrees, EndAngleDegrees, Mathf.Clamp(t, 0f, 1f));
    }

    private Color GetSegmentColor(ActionWheelCommandViewModel command)
    {
        if (!command.IsEnabled)
        {
            return new Color(0.2f, 0.2f, 0.2f, 0.22f);
        }

        if (command.Id == _activeCommandId)
        {
            return new Color(1f, 0.75f, 0.2f, 0.52f);
        }

        return new Color(0f, 0f, 0f, 0.34f);
    }

    private void DrawBackgroundArc(Vector2 center)
    {
        DrawEllipseRingSector(
            center,
            new Vector2(InnerRadiusX - 14f, InnerRadiusY - 6f),
            new Vector2(OuterRadiusX + 16f, OuterRadiusY + 12f),
            StartAngleDegrees - 8f,
            EndAngleDegrees + 8f,
            new Color(0.02f, 0.02f, 0.02f, 0.36f * _visualAlpha));
    }

    private void DrawEllipseRingSector(
        Vector2 center,
        Vector2 innerRadius,
        Vector2 outerRadius,
        float startDegrees,
        float endDegrees,
        Color color)
    {
        const int Steps = 22;
        var points = new List<Vector2>();

        for (int step = 0; step <= Steps; step++)
        {
            float angle = Mathf.DegToRad(Mathf.Lerp(startDegrees, endDegrees, step / (float)Steps));
            float angleDegrees = Mathf.RadToDeg(angle);
            float t = GetAngleT(angleDegrees);
            points.Add(BuildWhalePoint(center, outerRadius, angle, t, false, 1f));
        }

        for (int step = Steps; step >= 0; step--)
        {
            float angle = Mathf.DegToRad(Mathf.Lerp(startDegrees, endDegrees, step / (float)Steps));
            float angleDegrees = Mathf.RadToDeg(angle);
            float t = GetAngleT(angleDegrees);
            points.Add(BuildWhalePoint(center, innerRadius, angle, t, true, TailInnerDragScale));
        }

        DrawColoredPolygon(points.ToArray(), color);
        DrawPolyline(points.Append(points[0]).ToArray(), new Color(1f, 1f, 1f, 0.18f * color.A), 1.4f);
    }

    private Vector2 BuildWhalePoint(Vector2 center, Vector2 radius, float angle, float t, bool inner, float tailDragScale)
    {
        float tail = GetTailCompression(t);
        float horizontalProfile = GetWhaleHorizontalProfile(t, inner);
        float verticalProfile = inner
            ? Mathf.Lerp(GetWhaleInnerProfile(t), GetWhaleProfile(t), 0.46f)
            : GetWhaleProfile(t);

        Vector2 point = center + new Vector2(
            Mathf.Cos(angle) * radius.X * horizontalProfile,
            Mathf.Sin(angle) * radius.Y * verticalProfile);

        point.X += GetTailDirection() * TailDragLength * tailDragScale * tail;
        point.Y += TailDrop * tailDragScale * tail;
        return ApplyTransitionTransform(center, point);
    }

    private Vector2 ApplyTransitionTransform(Vector2 center, Vector2 point)
    {
        if (Mathf.IsEqualApprox(_transitionWidthScale, 1f) &&
            Mathf.IsEqualApprox(_transitionWheelScale, 1f))
        {
            return point;
        }

        point.X = center.X + (point.X - center.X) * _transitionWidthScale;
        point = center + (point - center) * _transitionWheelScale;
        return point;
    }

    private float GetTransitionWidthScale(float t)
    {
        if (t < LayerTransitionSwapRatio)
        {
            return 1f;
        }

        float expandT = Mathf.Clamp((t - LayerTransitionSwapRatio) / Mathf.Max(1f - LayerTransitionSwapRatio, 0.001f), 0f, 1f);
        float liftOff = GetRocketBurst(expandT);
        float pop = liftOff * LayerTransitionWidthBurst;
        float ripple = Mathf.Sin(expandT * Mathf.Pi * 4.5f) * Mathf.Pow(1f - expandT, 2.2f) * LayerTransitionWidthRipple;
        return Mathf.Max(0.96f, 1f + pop + ripple);
    }

    private float GetTransitionWheelScale(float t)
    {
        float swap = Mathf.Clamp(LayerTransitionSwapRatio, 0.2f, 0.8f);

        if (t < swap)
        {
            float collapseT = GetRocketEase(t / swap);
            return Mathf.Lerp(1f, LayerTransitionMinScale, collapseT);
        }

        float expandT = GetRocketEase((t - swap) / Mathf.Max(1f - swap, 0.001f));
        return Mathf.Lerp(LayerTransitionMinScale, 1f, expandT);
    }

    private float GetTransitionWheelAlpha(float t)
    {
        float swap = Mathf.Clamp(LayerTransitionSwapRatio, 0.2f, 0.8f);

        if (t < swap)
        {
            float local = Mathf.Clamp((t / swap - 0.08f) / 0.64f, 0f, 1f);
            return 1f - GetRocketEase(local);
        }

        float expandT = Mathf.Clamp((t - swap) / Mathf.Max(1f - swap, 0.001f), 0f, 1f);
        return GetRocketEase(Mathf.Clamp((expandT - 0.12f) / 0.76f, 0f, 1f));
    }

    private float GetTransitionSlotAlpha(float t)
    {
        if (!_transitionLayerSwapped || t < LayerTransitionSlotRevealRatio)
        {
            return 0f;
        }

        float local = Mathf.Clamp(
            (t - LayerTransitionSlotRevealRatio) / Mathf.Max(1f - LayerTransitionSlotRevealRatio, 0.001f),
            0f,
            1f);
        return GetRocketEase(local);
    }

    private float GetTransitionRotation(float t)
    {
        float swap = Mathf.Clamp(LayerTransitionSwapRatio, 0.2f, 0.8f);

        if (t < swap)
        {
            float collapseT = GetRocketEase(t / swap);
            return Mathf.Lerp(0f, -_transitionDirection * LayerTransitionOffsetDegrees * 0.28f, collapseT);
        }

        float expandT = GetRocketEase((t - swap) / Mathf.Max(1f - swap, 0.001f));
        return Mathf.Lerp(_transitionFromRotation, 0f, expandT);
    }

    private void DrawTransitionCore(Vector2 center)
    {
        if (!_isTransitioning)
        {
            return;
        }

        float t = Mathf.Clamp(_transitionElapsed / Mathf.Max(LayerTransitionDuration, 0.001f), 0f, 1f);
        float alpha = GetTransitionCoreAlpha(t);

        if (alpha <= 0.01f)
        {
            return;
        }

        float pulse = Mathf.Sin(t * Mathf.Pi);
        float radius = LayerTransitionCoreRadius * Mathf.Lerp(0.82f, 1.18f, pulse);
        float rotation = -Mathf.Pi * 0.5f + _transitionDirection * t * Mathf.Pi * 2.6f;

        Color bright = new(1f, 0.86f, 0.34f, 0.88f * alpha);
        Color dark = new(0.02f, 0.025f, 0.035f, 0.92f * alpha);
        Color border = new(1f, 1f, 1f, 0.34f * alpha);

        DrawCircle(center, radius + 3f, new Color(0f, 0f, 0f, 0.28f * alpha));
        DrawColoredPolygon(BuildPiePoints(center, radius, rotation - Mathf.Pi * 0.5f, rotation + Mathf.Pi * 0.5f), bright);
        DrawColoredPolygon(BuildPiePoints(center, radius, rotation + Mathf.Pi * 0.5f, rotation + Mathf.Pi * 1.5f), dark);

        Vector2 axis = new(Mathf.Cos(rotation), Mathf.Sin(rotation));
        DrawCircle(center + axis * radius * 0.5f, radius * 0.5f, dark);
        DrawCircle(center - axis * radius * 0.5f, radius * 0.5f, bright);
        DrawCircle(center + axis * radius * 0.5f, radius * 0.13f, bright);
        DrawCircle(center - axis * radius * 0.5f, radius * 0.13f, dark);

        Vector2[] outline = BuildCirclePoints(center, radius + 0.5f);
        DrawPolyline(outline.Append(outline[0]).ToArray(), border, 1.4f);
    }

    private float GetTransitionCoreAlpha(float t)
    {
        float swap = Mathf.Clamp(LayerTransitionSwapRatio, 0.2f, 0.8f);

        if (t < swap)
        {
            float local = Mathf.Clamp((t / swap - 0.12f) / 0.58f, 0f, 1f);
            return GetRocketEase(local);
        }

        float expandT = Mathf.Clamp((t - swap) / Mathf.Max(1f - swap, 0.001f), 0f, 1f);
        return 1f - GetRocketEase(Mathf.Clamp((expandT - 0.22f) / 0.58f, 0f, 1f));
    }

    private static Vector2[] BuildPiePoints(Vector2 center, float radius, float startAngle, float endAngle)
    {
        const int Steps = 18;
        var points = new List<Vector2> { center };

        for (int step = 0; step <= Steps; step++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, step / (float)Steps);
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return points.ToArray();
    }

    private static Vector2[] BuildCirclePoints(Vector2 center, float radius)
    {
        const int Steps = 44;
        var points = new Vector2[Steps];

        for (int step = 0; step < Steps; step++)
        {
            float angle = Mathf.Pi * 2f * step / Steps;
            points[step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return points;
    }

    private static float GetRocketEase(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float GetRocketBurst(float t)
    {
        float rise = GetRocketEase(Mathf.Clamp((t - 0.1f) / 0.42f, 0f, 1f));
        float fall = 1f - GetRocketEase(Mathf.Clamp((t - 0.52f) / 0.42f, 0f, 1f));
        return rise * fall;
    }

    private float GetWhaleHorizontalProfile(float t, bool inner)
    {
        float bodyDistance = Mathf.Abs(t - BodyCenterT);
        float body = BodyBulge * 0.72f * Mathf.Exp(-bodyDistance * bodyDistance / 0.055f);
        float tail = GetTailCompression(t);
        float tailNarrowing = inner ? 0.03f : 0.08f;
        float minScale = inner ? 0.86f : 0.9f;
        return Mathf.Clamp(1f + body - tail * tailNarrowing, minScale, 1.22f);
    }

    private float GetWhaleProfile(float t)
    {
        float bodyDistance = Mathf.Abs(t - BodyCenterT);
        float body = BodyBulge * Mathf.Exp(-bodyDistance * bodyDistance / 0.055f);
        float tail = GetTailCompression(t);
        return Mathf.Clamp(1f + body - tail * (1f - TailOuterScale), 0.42f, 1.28f);
    }

    private float GetWhaleInnerProfile(float t)
    {
        float tail = GetTailCompression(t);
        return Mathf.Clamp(1f - tail * (1f - TailInnerScale), 0.58f, 1.08f);
    }

    private float GetTailCompression(float t)
    {
        float tailRange = Mathf.Max(1f - TailStartT, 0.001f);
        float tail = Mathf.Clamp((t - TailStartT) / tailRange, 0f, 1f);
        return tail * tail * (3f - 2f * tail);
    }

    private float GetTailDirection()
    {
        float direction = Mathf.Sign(Mathf.Cos(Mathf.DegToRad(EndAngleDegrees)));
        return direction == 0f ? 1f : direction;
    }

    private float GetAngleT(float angleDegrees)
    {
        float range = Mathf.Max(EndAngleDegrees - StartAngleDegrees, 0.001f);
        return Mathf.Clamp((angleDegrees - StartAngleDegrees) / range, 0f, 1f);
    }
}
