using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.Battle.UI;

public partial class UnitStatusCard : Control
{
    private const int CurveSteps = 22;
    private const float BottomLeftInsetRatio = 0.68f;
    private const float LeftEdgeConcavityRatio = 0.025f;

    private readonly Label _portrait = new();
    private readonly Label _name = new();
    private readonly Label _hp = new();
    private readonly Label _ap = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(300, 132);

        _portrait.CustomMinimumSize = new Vector2(46, 46);
        _portrait.HorizontalAlignment = HorizontalAlignment.Center;
        _portrait.VerticalAlignment = VerticalAlignment.Center;
        _portrait.Text = "头像";
        _portrait.AddThemeFontSizeOverride("font_size", 13);
        _portrait.AddThemeColorOverride("font_color", Colors.White);
        _portrait.AddThemeStyleboxOverride("normal", BuildPortraitStyle());
        AddChild(_portrait);

        foreach (Label label in new[] { _name, _hp, _ap })
        {
            label.MouseFilter = MouseFilterEnum.Ignore;
            label.AddThemeColorOverride("font_color", Colors.White);
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            AddChild(label);
        }

        _name.AddThemeFontSizeOverride("font_size", 16);
        _hp.AddThemeFontSizeOverride("font_size", 13);
        _ap.AddThemeFontSizeOverride("font_size", 13);

        LayoutContent();
        SetUnit("骑士", 24, 24, 3, 3);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutContent();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2[] points = BuildPanelPoints();
        DrawColoredPolygon(OffsetPoints(points, new Vector2(0f, 4f)), new Color(0f, 0f, 0f, 0.16f));
        DrawColoredPolygon(points, new Color(0.02f, 0.02f, 0.02f, 0.4f));

        DrawPolyline(ClosePolygon(points), new Color(1f, 1f, 1f, 0.16f), 1.2f, true);
    }

    public void SetUnit(string unitName, int hp, int maxHp, int ap, int maxAp)
    {
        _name.Text = unitName;
        _hp.Text = $"生命 {hp}/{maxHp}";
        _ap.Text = $"行动点 {BuildApPips(ap, maxAp)}";
        QueueRedraw();
    }

    private static string BuildApPips(int ap, int maxAp)
    {
        if (maxAp <= 0)
        {
            return "-";
        }

        string pips = "";

        for (int index = 0; index < maxAp; index++)
        {
            pips += index < ap ? "●" : "○";
        }

        return pips;
    }

    private void LayoutContent()
    {
        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        float height = Mathf.Max(Size.Y, CustomMinimumSize.Y);
        float leftInset = GetBottomLeftInset(width);
        float portraitSize = Mathf.Clamp(height * 0.28f, 46f, 58f);
        float contentLeft = Mathf.Clamp(leftInset * 0.58f + 8f, 164f, 236f);
        float centerY = height * 0.5f;
        float textLeft = contentLeft + portraitSize + 12f;
        float textWidth = Mathf.Max(80f, width - textLeft - 18f);

        _portrait.Size = new Vector2(portraitSize, portraitSize);
        _portrait.Position = new Vector2(contentLeft, centerY - portraitSize * 0.5f);

        _name.Size = new Vector2(textWidth, 24f);
        _hp.Size = new Vector2(textWidth, 20f);
        _ap.Size = new Vector2(textWidth, 20f);

        _name.Position = new Vector2(textLeft, centerY - 40f);
        _hp.Position = new Vector2(textLeft, centerY - 9f);
        _ap.Position = new Vector2(textLeft, centerY + 17f);
    }

    private Vector2[] BuildPanelPoints()
    {
        float width = Mathf.Max(Size.X, 1f);
        float height = Mathf.Max(Size.Y, 1f);
        float bottomLeftInset = GetBottomLeftInset(width);

        var points = new List<Vector2>
        {
            Vector2.Zero,
            new(width, 0f),
            new(width, height),
            new(bottomLeftInset, height)
        };

        Vector2 edgeStart = new(bottomLeftInset, height);
        Vector2 edgeEnd = Vector2.Zero;
        float concavity = Mathf.Clamp(width * LeftEdgeConcavityRatio, 12f, 24f);

        for (int step = 1; step <= CurveSteps; step++)
        {
            float t = step / (float)CurveSteps;
            Vector2 point = edgeStart.Lerp(edgeEnd, t);
            point.X += Mathf.Sin(t * Mathf.Pi) * concavity;
            points.Add(point);
        }

        return points.ToArray();
    }

    private static float GetBottomLeftInset(float width)
    {
        return Mathf.Clamp(width * BottomLeftInsetRatio, 240f, 390f);
    }

    private static Vector2[] OffsetPoints(Vector2[] points, Vector2 offset)
    {
        var offsetPoints = new Vector2[points.Length];

        for (int index = 0; index < points.Length; index++)
        {
            offsetPoints[index] = points[index] + offset;
        }

        return offsetPoints;
    }

    private static Vector2[] ClosePolygon(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];

        for (int index = 0; index < points.Length; index++)
        {
            closed[index] = points[index];
        }

        closed[^1] = points[0];
        return closed;
    }

    private static StyleBoxFlat BuildPortraitStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.04f, 0.54f),
            BorderColor = new Color(1f, 1f, 1f, 0.24f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(24);
        return style;
    }
}
