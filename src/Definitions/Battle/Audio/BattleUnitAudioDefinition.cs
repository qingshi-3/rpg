using Godot;

namespace Rpg.Definitions.Battle.Audio;

[GlobalClass]
public partial class BattleUnitAudioDefinition : Resource
{
    [Export]
    public Godot.Collections.Array<AudioStream> DeploySounds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AudioStream> MoveSounds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AudioStream> AttackSounds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AudioStream> AttackImpactSounds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AudioStream> HitSounds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AudioStream> DefeatedSounds { get; set; } = new();

    public bool HasCue(BattleUnitAudioCue cue)
    {
        return GetCueSounds(cue).Count > 0;
    }

    public AudioStream ResolveCue(BattleUnitAudioCue cue, int variantIndex = 0)
    {
        Godot.Collections.Array<AudioStream> sounds = GetCueSounds(cue);
        if (sounds.Count == 0)
        {
            return null;
        }

        int index = ResolveVariantIndex(variantIndex, sounds.Count);
        return sounds[index];
    }

    public static int ResolveVariantIndex(int variantIndex, int variantCount)
    {
        if (variantCount <= 0)
        {
            return -1;
        }

        int index = variantIndex % variantCount;
        return index < 0
            ? index + variantCount
            : index;
    }

    private Godot.Collections.Array<AudioStream> GetCueSounds(BattleUnitAudioCue cue)
    {
        return cue switch
        {
            BattleUnitAudioCue.Deploy => DeploySounds,
            BattleUnitAudioCue.Move => MoveSounds,
            BattleUnitAudioCue.Attack => AttackSounds,
            BattleUnitAudioCue.AttackImpact => AttackImpactSounds,
            BattleUnitAudioCue.Hit => HitSounds,
            BattleUnitAudioCue.Defeated => DefeatedSounds,
            _ => AttackSounds
        } ?? new Godot.Collections.Array<AudioStream>();
    }
}
