using System;
using Godot;

namespace Rpg.Infrastructure.Scenes;

public sealed class GodotSceneTransitionGateway : ISceneTransitionGateway
{
    private readonly Func<SceneTree> _getSceneTree;

    public GodotSceneTransitionGateway(Func<SceneTree> getSceneTree)
    {
        _getSceneTree = getSceneTree;
    }

    public Error ChangeSceneToFile(string scenePath)
    {
        SceneTree tree = _getSceneTree?.Invoke();
        if (tree == null || string.IsNullOrWhiteSpace(scenePath))
        {
            return Error.Failed;
        }

        return tree.ChangeSceneToFile(scenePath);
    }
}
