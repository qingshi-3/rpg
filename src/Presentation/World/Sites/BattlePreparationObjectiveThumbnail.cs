using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Presentation.World.Sites;

public partial class BattlePreparationObjectiveThumbnail : Control
{
    [Signal]
    public delegate void ObjectiveZoneSelectedEventHandler(string objectiveZoneId);

    private Label _companyLabel;
    private BattleObjectiveMapPreview _mapPreview;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _companyLabel = GetNodeOrNull<Label>("Margin/Stack/CompanyLabel");
        _mapPreview = GetNodeOrNull<BattleObjectiveMapPreview>("Margin/Stack/MapPreview");
        if (_mapPreview != null)
        {
            _mapPreview.ObjectiveZoneSelected += zoneId =>
                EmitSignal(SignalName.ObjectiveZoneSelected, zoneId ?? "");
        }
    }

    public void Bind(
        string companyName,
        IReadOnlyList<BattleObjectiveMapCell> cells,
        IReadOnlyList<BattleObjectiveZoneSnapshot> zones,
        string selectedObjectiveZoneId,
        IReadOnlyList<BattleObjectiveMapRegion> regions)
    {
        if (_companyLabel != null)
        {
            _companyLabel.Text = string.IsNullOrWhiteSpace(companyName) ? "当前部队" : companyName.Trim();
        }

        _mapPreview?.SetData(cells, zones, selectedObjectiveZoneId, regions);
    }
}
