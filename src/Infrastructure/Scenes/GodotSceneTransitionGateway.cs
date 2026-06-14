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

    public Error ChangeSceneToFile(string scenePath, Action onSceneEntered)
    {
        SceneTree tree = _getSceneTree?.Invoke();
        if (tree == null || string.IsNullOrWhiteSpace(scenePath))
        {
            return Error.Failed;
        }

        void HandleSceneChanged()
        {
            tree.SceneChanged -= HandleSceneChanged;
            onSceneEntered?.Invoke();
        }

        tree.SceneChanged += HandleSceneChanged;
        Error error = tree.ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            tree.SceneChanged -= HandleSceneChanged;
        }

        return error;
    }
}
