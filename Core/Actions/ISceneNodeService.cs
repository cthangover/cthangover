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
        void Instantiate(string scenePath, string nodeName);
        void Remove(string nodeName);
        T Find<T>(string nodeName) where T : Node;
    }
}
