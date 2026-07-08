using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World.Sites;

internal enum BattleMapOperationHudSuppressionReason
{
    None,
    PreparationPlacement,
    PreparationDestinationBeacon,
    RuntimeDestinationBeacon,
    RuntimeSkillTarget
}

internal sealed class BattleMapOperationHudSuppressor
{
    private readonly string _ownerName;
    private readonly Dictionary<Control, ControlRestoreState> _restoreStates = new();

    internal BattleMapOperationHudSuppressor(string ownerName)
    {
        _ownerName = string.IsNullOrWhiteSpace(ownerName) ? nameof(BattleMapOperationHudSuppressor) : ownerName;
    }

    internal BattleMapOperationHudSuppressionReason Enter(
        BattleMapOperationHudSuppressionReason activeReason,
        BattleMapOperationHudSuppressionReason requestedReason,
        IEnumerable<Control> controls,
        string diagnosticReason)
    {
        if (requestedReason == BattleMapOperationHudSuppressionReason.None)
        {
            return activeReason;
        }

        bool wasInactive = activeReason == BattleMapOperationHudSuppressionReason.None;
        if (wasInactive)
        {
            _restoreStates.Clear();
        }

        Apply(requestedReason, controls, diagnosticReason);
        GameLog.Info(
            _ownerName,
            $"BattleMapOperationHudSuppressionEntered reason={requestedReason} previousInactive={wasInactive} diagnostic={diagnosticReason ?? ""}");
        return requestedReason;
    }

    internal BattleMapOperationHudSuppressionReason Exit(
        BattleMapOperationHudSuppressionReason activeReason,
        BattleMapOperationHudSuppressionReason requestedReason,
        string diagnosticReason)
    {
        if (activeReason == BattleMapOperationHudSuppressionReason.None)
        {
            return activeReason;
        }

        if (requestedReason != BattleMapOperationHudSuppressionReason.None &&
            activeReason != requestedReason)
        {
            GameLog.Info(
                _ownerName,
                $"BattleMapOperationHudSuppressionExitIgnored active={activeReason} requested={requestedReason} diagnostic={diagnosticReason ?? ""}");
            return activeReason;
        }

        foreach ((Control control, ControlRestoreState restore) in _restoreStates)
        {
            if (control == null || !GodotObject.IsInstanceValid(control))
            {
                continue;
            }

            control.Visible = restore.Visible;
            control.MouseFilter = restore.MouseFilter;
        }

        _restoreStates.Clear();
        GameLog.Info(
            _ownerName,
            $"BattleMapOperationHudSuppressionExited reason={activeReason} diagnostic={diagnosticReason ?? ""}");
        return BattleMapOperationHudSuppressionReason.None;
    }

    internal void Apply(
        BattleMapOperationHudSuppressionReason activeReason,
        IEnumerable<Control> controls,
        string diagnosticReason)
    {
        if (activeReason == BattleMapOperationHudSuppressionReason.None)
        {
            return;
        }

        foreach (Control control in controls ?? System.Array.Empty<Control>())
        {
            Suppress(control);
        }

        GameLog.Info(
            _ownerName,
            $"BattleMapOperationHudSuppressionApplied reason={activeReason} diagnostic={diagnosticReason ?? ""}");
    }

    private void Suppress(Control control)
    {
        if (control == null || !GodotObject.IsInstanceValid(control))
        {
            return;
        }

        _restoreStates.TryAdd(control, new ControlRestoreState(control.Visible, control.MouseFilter));
        control.Visible = false;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private readonly struct ControlRestoreState
    {
        internal ControlRestoreState(bool visible, Control.MouseFilterEnum mouseFilter)
        {
            Visible = visible;
            MouseFilter = mouseFilter;
        }

        internal bool Visible { get; }
        internal Control.MouseFilterEnum MouseFilter { get; }
    }
}
