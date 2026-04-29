using Godot;

namespace Rpg.Presentation.World;

public partial class WorldPlayer : CharacterBody2D
{
    [Export]
    public float Speed { get; set; } = 120.0f;

    public override void _PhysicsProcess(double delta)
    {
        Vector2 direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            direction.X -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            direction.X += 1.0f;
        }

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            direction.Y -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            direction.Y += 1.0f;
        }

        Velocity = direction == Vector2.Zero ? Vector2.Zero : direction.Normalized() * Speed;
        MoveAndSlide();
    }
}

