using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitAudioComponent : BattleEntityComponent
{
    [Export]
    public BattleUnitAudioDefinition Audio { get; set; }

    [Export]
    public NodePath AudioPlayerPath { get; set; } = new("AudioStreamPlayer2D");

    [Export]
    public float VolumeDb { get; set; } = -3f;

    [Export]
    public float PitchScale { get; set; } = 1f;

    private AudioStreamPlayer2D _player;
    private int _variantIndex;

    protected override void OnAttached()
    {
        ResolvePlayer();
    }

    public override void _ExitTree()
    {
        _player = null;
    }

    public void PlayCue(BattleUnitAudioCue cue)
    {
        if (Audio == null)
        {
            return;
        }

        AudioStream stream = Audio.ResolveCue(cue, _variantIndex++);
        if (stream == null)
        {
            return;
        }

        AudioStreamPlayer2D player = ResolvePlayer();
        if (player == null)
        {
            GameLog.Warn(nameof(BattleUnitAudioComponent), $"Missing audio player entity={Entity?.EntityId} cue={cue}");
            return;
        }

        player.Stream = stream;
        player.VolumeDb = VolumeDb;
        player.PitchScale = PitchScale;
        player.Play();
        GameLog.Info(nameof(BattleUnitAudioComponent), $"Audio cue played entity={Entity?.EntityId} cue={cue}");
    }

    private AudioStreamPlayer2D ResolvePlayer()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            return _player;
        }

        _player = GetNodeOrNull<AudioStreamPlayer2D>(AudioPlayerPath);
        return _player;
    }
}
