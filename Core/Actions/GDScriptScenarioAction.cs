using Godot;

namespace Cthangover.Core.Actions;

/// <summary>
/// Adapter that exposes a GDScript object with <c>get_name()</c> + <c>run(ctx)</c>
/// as a regular <see cref="IScenarioAction"/>. Created by
/// <c>ModGDScriptLoader</c> for every matching <c>.gd</c> file and registered in
/// <see cref="Cthangover.Core.Factories.Impls.ScenarioActionFactory"/> like any
/// C# action, so the DSL <c>action</c> command cannot tell the two apart.
/// </summary>
public class GDScriptScenarioAction : IScenarioAction
{
    private readonly GodotObject _instance;

    /// <summary>
    /// Wraps a compiled GDScript instance.
    /// </summary>
    /// <param name="name">Action name returned by the script's <c>get_name()</c>; becomes the DSL registry key.</param>
    /// <param name="instance">Live GodotObject backing the GDScript; <c>run(ctx)</c> is invoked on it.</param>
    public GDScriptScenarioAction(string name, GodotObject instance)
    {
        Name = name;
        _instance = instance;
    }

    /// <summary>DSL action name from the script's <c>get_name()</c> (e.g. "cooking.meal_cooked").</summary>
    public string Name { get; }

    /// <summary>
    /// Invokes the script's <c>run(ctx)</c> with a <see cref="GDActionContext"/>
    /// bound to the C# context, giving the script access to dialog variables
    /// and all subsystem services.
    /// </summary>
    public void Run(IActionContext ctx)
    {
        var gdCtx = new GDActionContext();
        gdCtx.Core.Inner = ctx;
        _instance.Call("run", gdCtx);
    }
}
