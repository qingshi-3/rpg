using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Definitions.World;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed partial class EmotionSystem : IEmotionSystem
{
    private const int MinValue = -100;
    private const int MaxValue = 100;

    private EmotionWorldState _state;
    private readonly Dictionary<string, RaceEmotionProfileDefinition> _raceProfiles = new();
    private readonly Dictionary<string, EmotionProfileModifierDefinition> _modifiers = new();
    private readonly Dictionary<string, EmotionEventDefinition> _eventDefinitions = new();
    private readonly int _runSeed;

    public EmotionSystem(EmotionDefinitionDatabase database, int runSeed = 0, EmotionWorldState state = null)
    {
        _runSeed = runSeed;
        _state = state ?? new EmotionWorldState();
        IndexDefinitions(database);
    }
}
