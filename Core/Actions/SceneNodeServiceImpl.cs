using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Actions
{
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
