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
    private sealed class EmotionSocialContext
    {
        public EmotionSocialContext(
            EmotionActorState actor,
            EmotionRelationshipState relationship,
            EmotionDispositionResult disposition)
        {
            Actor = actor;
            Relationship = relationship;
            Disposition = disposition;
        }


        public EmotionActorState Actor { get; }
        public EmotionRelationshipState Relationship { get; }
        public EmotionDispositionResult Disposition { get; }

        public int Trust => Relationship.Get(EmotionRelationshipMetric.Trust);
        public int Affinity => Relationship.Get(EmotionRelationshipMetric.Affinity);
        public int Fear => Relationship.Get(EmotionRelationshipMetric.Fear);
        public int Respect => Relationship.Get(EmotionRelationshipMetric.Respect);
        public int Grievance => Relationship.Get(EmotionRelationshipMetric.Grievance);
    }
}
