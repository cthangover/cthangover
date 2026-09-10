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
        /// <summary>
        /// Loads a PackedScene by resource path via GD.Load, instantiates
        /// it, assigns the node name, and adds it as a child of
        /// SceneContextNode.Instance (the current scene's autoload root).
        /// If the resource fails to load (wrong path, missing file), logs
        /// an error and returns without throwing — the dialog continues.
        /// The instantiated node becomes visible immediately.
        /// </summary>
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

        /// <summary>
        /// Removes a named child node from SceneContextNode.Instance.
        /// First notifies the event system via RemoveEventObject to clean
        /// up any event subscriptions referencing the node, then performs
        /// RemoveChild + QueueFree. Safe to call on non-existent nodes
        /// (silently returns if the context or child is null).
        /// </summary>
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

        /// <summary>
        /// Typed recursive lookup by node name across the entire scene
        /// tree via SceneContextNode.FindNode. Returns null if no
        /// matching node of type T with the given name exists. Used by
        /// TogglePanelAction to locate UI controls at any nesting depth.
        /// </summary>
        public T Find<T>(string nodeName) where T : Node
        {
            return SceneContextNode.FindNode<T>(nodeName);
        }
    }
}
