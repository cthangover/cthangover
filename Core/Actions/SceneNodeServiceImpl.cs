using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Scene node operations implementation. Instantiate loads a PackedScene by
    /// path and attaches it to SceneContextNode.Instance — the current scene's
    /// autoload root. Remove delegates to SceneContextNode.RemoveEventObject
    /// before the raw RemoveChild+QueueFree, suggesting the event system tracks
    /// instantiated objects and needs cleanup notification. Find uses
    /// SceneContextNode.FindNode for typed recursive lookup across the scene tree.
    /// </summary>
    internal class SceneNodeServiceImpl : ISceneNodeService
    {
        public void Instantiate(string scenePath, string nodeName)
        {
            var packedScene = GD.Load<PackedScene>(scenePath);
            if (packedScene == null)
            {
                GameLogger.Log("SCENE", $"SceneNodeService: failed to load '{scenePath}'", LogLevel.Error);
                return;
            }

            var instance = packedScene.Instantiate();
            instance.Name = nodeName;
            SceneContextNode.Instance?.AddChild(instance);
        }

        public void Remove(string nodeName)
        {
            var ctx = SceneContextNode.Instance;
            if (ctx == null)
                return;

            ctx.RemoveEventObject(nodeName);

            var child = ctx.FindChild(nodeName, false, false);
            if (child != null)
            {
                ctx.RemoveChild(child);
                child.QueueFree();
            }
        }

        public T Find<T>(string nodeName) where T : Node
        {
            return SceneContextNode.FindNode<T>(nodeName);
        }
    }
}
