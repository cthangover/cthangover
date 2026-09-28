using System.Collections.Generic;
using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

public class MockActionContext : IActionContext
{
    private readonly Dictionary<string, string> _params = new();
    public List<(string Category, string Message)> Logs { get; } = new();
    public List<(string Category, string Message)> Warnings { get; } = new();

    public IQuestService Quests { get; set; }
    public ICharacterService Character { get; set; }
    public ILightingService Lighting { get; set; }
    public ISceneNodeService Scene { get; set; }
    public Cthangover.Core.Mods.IModRegistry ModRegistry { get; set; }
    public Cthangover.Core.Items.IInventory Inventory { get; set; }

    public string GetParam(string name)
    {
        _params.TryGetValue(name, out var value);
        return value;
    }

    public void SetParam(string name, string value) => _params[name] = value;

    public void Log(string category, string message) => Logs.Add((category, message));
    public void LogWarning(string category, string message) => Warnings.Add((category, message));
}
