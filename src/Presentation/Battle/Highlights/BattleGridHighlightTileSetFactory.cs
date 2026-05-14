using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.Battle;

internal static class BattleGridHighlightTileSetFactory
{
    public const int SourceId = 0;

    public static BattleGridHighlightTileSetSpec Create(
        TileSet templateTileSet,
        IReadOnlyDictionary<BattleGridHighlightKind, BattleGridHighlightStyle> stylesByKind,
        IReadOnlyList<BattleGridHighlightKind> drawOrder)
    {
        Vector2I tileSize = ResolveTileSize(templateTileSet);
        Image atlas = Image.CreateEmpty(tileSize.X * drawOrder.Count, tileSize.Y, false, Image.Format.Rgba8);
        atlas.Fill(Colors.Transparent);

        Dictionary<BattleGridHighlightKind, BattleGridHighlightTile> tilesByKind = new();
        for (int index = 0; index < drawOrder.Count; index++)
        {
            BattleGridHighlightKind kind = drawOrder[index];
            if (!stylesByKind.TryGetValue(kind, out BattleGridHighlightStyle style))
            {
                continue;
            }

            DrawHighlightTile(atlas, tileSize, index, style);
            tilesByKind[kind] = new BattleGridHighlightTile(SourceId, new Vector2I(index, 0));
        }

        ImageTexture texture = ImageTexture.CreateFromImage(atlas);
        var source = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = tileSize
        };

        for (int index = 0; index < drawOrder.Count; index++)
        {
            source.CreateTile(new Vector2I(index, 0));
        }

        var tileSet = new TileSet
        {
            TileSize = tileSize
        };
        CopyTileSetLayout(templateTileSet, tileSet);
        tileSet.AddSource(source, SourceId);

        return new BattleGridHighlightTileSetSpec(tileSet, tilesByKind);
    }

    private static Vector2I ResolveTileSize(TileSet templateTileSet)
    {
        Vector2I tileSize = templateTileSet?.TileSize ?? new Vector2I(128, 64);
        return new Vector2I(System.Math.Max(8, tileSize.X), System.Math.Max(8, tileSize.Y));
    }

    private static void CopyTileSetLayout(TileSet source, TileSet target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.TileShape = source.TileShape;
        target.TileLayout = source.TileLayout;
        target.TileOffsetAxis = source.TileOffsetAxis;
    }

    private static void DrawHighlightTile(Image atlas, Vector2I tileSize, int tileIndex, BattleGridHighlightStyle style)
    {
        int originX = tileIndex * tileSize.X;
        float centerX = originX + (tileSize.X - 1) * 0.5f;
        float centerY = (tileSize.Y - 1) * 0.5f;
        float halfWidth = System.Math.Max(1f, tileSize.X * 0.5f - 1f);
        float halfHeight = System.Math.Max(1f, tileSize.Y * 0.5f - 1f);
        float borderWidth = Mathf.Max(1f, style.BorderWidth);

        for (int y = 0; y < tileSize.Y; y++)
        {
            for (int x = 0; x < tileSize.X; x++)
            {
                float normalizedDistance = Mathf.Abs(originX + x - centerX) / halfWidth +
                                           Mathf.Abs(y - centerY) / halfHeight;
                if (normalizedDistance > 1f)
                {
                    continue;
                }

                float edgeDistance = (1f - normalizedDistance) * System.Math.Min(halfWidth, halfHeight);
                Color color = edgeDistance <= borderWidth ? style.Border : style.Fill;
                atlas.SetPixel(originX + x, y, color);
            }
        }
    }
}

