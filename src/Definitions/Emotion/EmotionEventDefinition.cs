using Godot;

namespace Rpg.Definitions.Emotion;

[GlobalClass]
public partial class EmotionEventDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "情感事件";

    [Export]
    public EmotionEventKind Kind { get; set; } = EmotionEventKind.System;

    [Export]
    public Godot.Collections.Array<EmotionConditionDefinition> Conditions { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EmotionEffectDefinition> Effects { get; set; } = new();
}
