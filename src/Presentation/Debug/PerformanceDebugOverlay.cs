using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Godot;

namespace Rpg.Presentation.Debug;

public partial class PerformanceDebugOverlay : CanvasLayer
{
    [Export]
    public bool VisibleOnStart { get; set; } = true;

    [Export]
    public double SampleIntervalSeconds { get; set; } = 0.5;

    [Export]
    public Key ToggleKey { get; set; } = Key.F3;

    private readonly StringBuilder _builder = new(1024);
    private Label _label;
    private double _sampleAccumulator;
    private int _lastGc0;
    private int _lastGc1;
    private int _lastGc2;

    public override void _Ready()
    {
        Layer = 1000;
        Visible = VisibleOnStart;
        _label = GetNodeOrNull<Label>("Panel/Margin/Text");
        SetProcess(true);
        RefreshText();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.Keycode != ToggleKey && keyEvent.PhysicalKeycode != ToggleKey)
        {
            return;
        }

        Visible = !Visible;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _sampleAccumulator += delta;
        if (_sampleAccumulator < Math.Max(0.1, SampleIntervalSeconds))
        {
            return;
        }

        _sampleAccumulator = 0.0;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_label == null)
        {
            return;
        }

        Process process = Process.GetCurrentProcess();
        long managedBytes = GC.GetTotalMemory(forceFullCollection: false);
        int gc0 = GC.CollectionCount(0);
        int gc1 = GC.CollectionCount(1);
        int gc2 = GC.CollectionCount(2);

        _builder.Clear();
        _builder.AppendLine("PERFORMANCE");
        AppendMonitor("FPS", "TimeFps", "0");
        AppendMonitorMs("Frame", "TimeProcess");
        AppendMonitorMs("Physics", "TimePhysicsProcess");
        AppendMonitorBytes("Godot static", "MemoryStatic");
        AppendMonitorBytes("Godot static max", "MemoryStaticMax");
        AppendMonitorBytes("Message buffer max", "MemoryMessageBufferMax");
        AppendMonitor("Nodes", "ObjectNodeCount", "0");
        AppendMonitor("Resources", "ObjectResourceCount", "0");
        AppendMonitor("Orphan nodes", "ObjectOrphanNodeCount", "0");
        AppendMonitor("Draw calls", "RenderTotalDrawCallsInFrame", "0");
        AppendMonitor("Objects", "RenderTotalObjectsInFrame", "0");
        AppendMonitor("Primitives", "RenderTotalPrimitivesInFrame", "0");
        _builder.AppendLine($"Managed heap: {FormatBytes(managedBytes)}");
        _builder.AppendLine($"Working set: {FormatBytes(process.WorkingSet64)}");
        _builder.AppendLine($"Private mem: {FormatBytes(process.PrivateMemorySize64)}");
        _builder.AppendLine($"GC delta: gen0 +{gc0 - _lastGc0}  gen1 +{gc1 - _lastGc1}  gen2 +{gc2 - _lastGc2}");
        _builder.AppendLine($"F3: {(Visible ? "hide" : "show")}");

        _lastGc0 = gc0;
        _lastGc1 = gc1;
        _lastGc2 = gc2;
        _label.Text = _builder.ToString();
    }

    private void AppendMonitor(string label, string monitorName, string format)
    {
        if (TryGetMonitor(monitorName, out double value))
        {
            _builder.AppendLine($"{label}: {value.ToString(format, CultureInfo.InvariantCulture)}");
        }
    }

    private void AppendMonitorMs(string label, string monitorName)
    {
        if (TryGetMonitor(monitorName, out double value))
        {
            _builder.AppendLine($"{label}: {(value * 1000.0).ToString("0.00", CultureInfo.InvariantCulture)} ms");
        }
    }

    private void AppendMonitorBytes(string label, string monitorName)
    {
        if (TryGetMonitor(monitorName, out double value))
        {
            _builder.AppendLine($"{label}: {FormatBytes((long)value)}");
        }
    }

    private static bool TryGetMonitor(string monitorName, out double value)
    {
        value = 0.0;
        if (!Enum.TryParse(monitorName, out Performance.Monitor monitor))
        {
            return false;
        }

        value = Performance.GetMonitor(monitor);
        return true;
    }

    private static string FormatBytes(long bytes)
    {
        const double kib = 1024.0;
        const double mib = kib * 1024.0;
        const double gib = mib * 1024.0;

        return Math.Abs(bytes) switch
        {
            >= (long)gib => $"{bytes / gib:0.00} GiB",
            >= (long)mib => $"{bytes / mib:0.00} MiB",
            >= (long)kib => $"{bytes / kib:0.00} KiB",
            _ => $"{bytes} B"
        };
    }
}
