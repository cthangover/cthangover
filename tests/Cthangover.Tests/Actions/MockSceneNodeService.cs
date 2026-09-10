using Cthangover.Core.Actions;
using Godot;

namespace Engine.Tests.Actions;

internal class MockSceneNodeService : ISceneNodeService
{
    public string LastInstPath { get; private set; }
    public string LastInstName { get; private set; }
    public string LastRemoveName { get; private set; }

    public void Instantiate(string scenePath, string nodeName)
    {
        LastInstPath = scenePath;
        LastInstName = nodeName;
    }

    public void Remove(string nodeName) => LastRemoveName = nodeName;

    public T Find<T>(string nodeName) where T : Node => null;
}
