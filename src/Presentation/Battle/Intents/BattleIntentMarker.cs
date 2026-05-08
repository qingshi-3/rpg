using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.Intents;

public partial class BattleIntentMarker : Node2D
{
    [Export]
    public Vector2 MarkerOffset { get; set; } = new(10f, -25f);

    [Export]
    public Vector2 MarkerSize { get; set; } = new(18f, 18f);

    [Export]
    public float IconPadding { get; set; } = 2f;

    private Control _root;
    private TextureRect _iconTexture;
    private Label _fallbackIconLabel;
    private BattleIntent _intent;

    public override void _Ready()
    {
        Bind();
        ApplyIntent(_intent);
    }

    public void SetIntent(BattleIntent intent)
    {
        _intent = intent;
        Bind();
        ApplyIntent(intent);
    }

    private void ApplyIntent(BattleIntent intent)
    {
        if (_root == null || _iconTexture == null || _fallbackIconLabel == null)
        {
            return;
        }

        if (intent == null)
        {
            Visible = false;
            return;
        }

        Texture2D iconTexture = BattleIntentIcons.LoadTexture(intent.IconKey);
        _iconTexture.Texture = iconTexture;
        _iconTexture.Visible = iconTexture != null;
        _fallbackIconLabel.Text = "?";
        _fallbackIconLabel.Visible = iconTexture == null;
        _root.TooltipText = intent.Summary;
        Visible = true;
    }

    private void Bind()
    {
        if (_root != null)
        {
            return;
        }

        ZIndex = 400;
        _root = GameUiSceneFactory.GetRequiredNode<Control>(this, "Root", nameof(BattleIntentMarker));
        _iconTexture = GameUiSceneFactory.GetRequiredNode<TextureRect>(this, "Root/IconTexture", nameof(BattleIntentMarker));
        _fallbackIconLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "Root/FallbackIconLabel", nameof(BattleIntentMarker));
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_root == null)
        {
            return;
        }

        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.CustomMinimumSize = MarkerSize;
        _root.Size = MarkerSize;
        _root.Position = MarkerOffset - MarkerSize * 0.5f;

        Vector2 iconSize = new(
            System.Math.Max(1f, MarkerSize.X - IconPadding * 2f),
            System.Math.Max(1f, MarkerSize.Y - IconPadding * 2f));

        if (_iconTexture != null)
        {
            _iconTexture.CustomMinimumSize = iconSize;
            _iconTexture.Size = iconSize;
        }

        if (_fallbackIconLabel != null)
        {
            _fallbackIconLabel.CustomMinimumSize = iconSize;
            _fallbackIconLabel.Size = iconSize;
        }
    }
}
