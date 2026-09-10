using Godot;

namespace Cthangover.Core.Actions;

public class GDScriptScenarioAction : IScenarioAction
{
    private readonly GodotObject _instance;

    public GDScriptScenarioAction(string name, GodotObject instance)
    {
        Name = name;
        _instance = instance;
    }

    public string Name { get; }

    public void Run(IActionContext ctx)
    {
        var gdCtx = new GDActionContext();
        gdCtx.Core.Inner = ctx;
        _instance.Call("run", gdCtx);
    }
}
