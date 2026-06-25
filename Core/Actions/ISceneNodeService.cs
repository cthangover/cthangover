using Godot;

namespace Cthangover.Core.Actions
{
    public interface ISceneNodeService
    {
        void Instantiate(string scenePath, string nodeName);
        void Remove(string nodeName);
        T Find<T>(string nodeName) where T : Node;
    }
}
