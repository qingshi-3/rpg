using System.Collections.Generic;

namespace Rpg.Domain.Heroes;

public sealed class HeroState
{
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "player";
    public int Level { get; set; } = 1;
    public HeroRank Rank { get; set; } = HeroRank.Ordinary;
    public HeroAttributeSet BaseAttributes { get; set; } = new();
    public List<string> SkillIds { get; set; } = new();
    public bool IsAvailable { get; set; } = true;
}
