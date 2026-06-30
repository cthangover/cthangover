using Godot;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Scene node manipulation contract for scenario actions. Instantiate loads
    /// a PackedScene and adds it as a child of SceneContextNode.Instance (the
    /// current scene root). Remove finds a child by name and frees it. Find
    /// provides typed lookup — used by TogglePanelAction to toggle Control
    /// visibility in the scene.
    /// </summary>
    public interface ISceneNodeService
    {
        /// <summary>
        /// Loads a PackedScene from the given resource path via
        /// GD.Load&lt;PackedScene&gt;, instantiates it, assigns the given
        /// node name, and attaches it as a child of
        /// SceneContextNode.Instance (the current scene's autoload root).
        /// The node becomes visible immediately. Logs an error if the
        /// resource fails to load (wrong path, missing file) rather than
        /// throwing — this keeps the dialog running even with broken
        /// scene references.
        /// </summary>
        void Instantiate(string scenePath, string nodeName);

        /// <summary>
        /// Removes a named child node from the scene tree and frees it.
        /// First calls SceneContextNode.RemoveEventObject to notify the
        /// event system of the pending destruction (prevents stale event
        /// references), then performs RemoveChild + QueueFree. Safe to
        /// call on non-existent nodes — silently returns.
        /// </summary>
        void Remove(string nodeName);

        /// <summary>
        /// Performs a typed recursive search for a node by name across the
        /// entire scene tree, starting from SceneContextNode.Instance.
        /// Returns null if no matching node of type T with the given name
        /// exists. Used by actions like TogglePanelAction to locate UI
        /// controls regardless of nesting depth.
        /// </summary>
        T Find<T>(string nodeName) where T : Node;
    }
}
