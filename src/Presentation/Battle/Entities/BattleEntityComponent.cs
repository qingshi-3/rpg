using Godot;

namespace Rpg.Presentation.Battle.Entities;

public abstract partial class BattleEntityComponent : Node
{
    public BattleEntity Entity { get; private set; }

    internal void AttachTo(BattleEntity entity)
    {
        Entity = entity;
        OnAttached();
    }

    protected virtual void OnAttached()
    {
    }
}
