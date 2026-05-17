using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Definitions.World;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed partial class EmotionSystem
{
    public EmotionWorldState ExportState()
    {
        return _state.Clone();
    }

    public EmotionOperationResult<EmotionWorldState> UseState(EmotionWorldState state)
    {
        if (state == null)
        {
            return EmotionOperationResult<EmotionWorldState>.Fail("Emotion world state is null.");
        }

        _state = state.Clone();
        return EmotionOperationResult<EmotionWorldState>.Ok(ExportState());
    }

    private void IndexDefinitions(EmotionDefinitionDatabase database)
    {
        _raceProfiles.Clear();
        _modifiers.Clear();
        _eventDefinitions.Clear();

        if (database == null)
        {
            return;
        }

        foreach (RaceEmotionProfileDefinition raceProfile in database.RaceProfiles)
        {
            if (raceProfile == null || string.IsNullOrWhiteSpace(raceProfile.RaceId))
            {
                continue;
            }

            _raceProfiles[raceProfile.RaceId] = raceProfile;
        }

        foreach (EmotionProfileModifierDefinition modifier in database.ProfileModifiers)
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.Id))
            {
                continue;
            }

            _modifiers[modifier.Id] = modifier;
        }

        foreach (EmotionEventDefinition eventDefinition in database.EventDefinitions)
        {
            if (eventDefinition == null || string.IsNullOrWhiteSpace(eventDefinition.Id))
            {
                continue;
            }

            _eventDefinitions[eventDefinition.Id] = eventDefinition;
        }
    }
}
