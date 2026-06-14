using System;
using Godot;

namespace Rpg.Infrastructure.Scenes;

public interface ISceneTransitionGateway
{
    Error ChangeSceneToFile(string scenePath, Action onSceneEntered);
}
