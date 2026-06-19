using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    private void RebuildDynamicOverlay()
    {
        ClearDynamicOverlay();

        if (_coordinateLayer == null)
        {
            return;
        }

        _vectorHighlightRenderer.Render(
            this,
            _vectorOverlayRoot,
            _highlightGeometry,
            _cellsByKind,
            _pathCells,
            _hoverCells,
            _tacticalPauseVisualsStatic);
    }

    private void ClearDynamicOverlay()
    {
        EnsureVectorOverlayRoot();
        foreach (Node child in _vectorOverlayRoot.GetChildren())
        {
            _vectorOverlayRoot.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void EnsureVectorOverlayRoot()
    {
        if (_vectorOverlayRoot != null && GodotObject.IsInstanceValid(_vectorOverlayRoot))
        {
            return;
        }

        _vectorOverlayRoot = GetNodeOrNull<Node2D>("RuntimeVectorOverlay");
        if (_vectorOverlayRoot != null)
        {
            return;
        }

        _vectorOverlayRoot = new Node2D
        {
            Name = "RuntimeVectorOverlay",
            // Child z-indices align with highlight kinds; a neutral root keeps range fills below target locks and hover frames.
            ZIndex = 0
        };
        AddChild(_vectorOverlayRoot);
    }
}