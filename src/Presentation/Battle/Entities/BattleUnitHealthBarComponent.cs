using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitHealthBarComponent : BattleEntityComponent
{
    [Export]
    public NodePath RootPath { get; set; } = new("../HealthBarRoot");

    [Export]
    public NodePath FillPath { get; set; } = new("../HealthBarRoot/HealthFill");

    [Export]
    public Vector2 BarOffset { get; set; } = new(-24f, -58f);

    [Export]
    public Vector2 BarSize { get; set; } = new(48f, 6f);

    private HealthComponent _health;
    private Control _root;
    private ColorRect _fill;

    protected override void OnAttached()
    {
        ResolveNodes();
        BindHealth();
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.HealthChanged -= OnHealthChanged;
        }

        _health = null;
        _root = null;
        _fill = null;
    }

    private void ResolveNodes()
    {
        _root = ResolveSibling<Control>(RootPath);
        _fill = ResolveSibling<ColorRect>(FillPath);
        if (_root == null)
        {
            return;
        }

        // The bar is authored in the unit scene; this component only enforces
        // stable runtime dimensions so HP changes cannot resize surrounding UI.
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.Position = BarOffset;
        _root.Size = BarSize;
    }

    private void BindHealth()
    {
        _health = Entity?.GetComponent<HealthComponent>();
        if (_health != null)
        {
            _health.HealthChanged += OnHealthChanged;
        }
    }

    private void OnHealthChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_root == null || _fill == null || _health == null)
        {
            return;
        }

        int maxHp = System.Math.Max(1, _health.MaxHp);
        float ratio = Mathf.Clamp(_health.Hp / (float)maxHp, 0f, 1f);
        Vector2 innerSize = new(
            Mathf.Max(0f, (BarSize.X - 2f) * ratio),
            Mathf.Max(1f, BarSize.Y - 2f));

        // Defeat is emitted after HealthChanged, so hiding here removes the HP bar
        // on the zero-HP frame before BattleUnitRoot starts the death animation.
        _root.Visible = maxHp > 1 && !_health.IsDead;
        _fill.Position = new Vector2(1f, 1f);
        _fill.Size = innerSize;
        _root.QueueRedraw();
    }

    private T ResolveSibling<T>(NodePath path) where T : Node
    {
        string value = path?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? null
            : GetNodeOrNull<T>(path);
    }
}
