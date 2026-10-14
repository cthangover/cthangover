using Godot;

namespace Cthangover.Core.Mods;

/// <summary>
/// Wraps a compiled GDScript object as an <see cref="IModInitializer"/> so a
/// <c>.gd</c> file can receive the same lifecycle callbacks as a C# initializer.
/// Each callback is forwarded only when the script actually defines the
/// corresponding method, so a script may implement either or both hooks.
/// </summary>
public class GDScriptModInitializer : IModInitializer
{
    private readonly GodotObject _instance;

    /// <summary>Wraps the given compiled GDScript instance.</summary>
    public GDScriptModInitializer(GodotObject instance)
    {
        _instance = instance;
    }

    /// <summary>Calls <c>on_mod_loaded(modId)</c> on the script when it defines it.</summary>
    public void OnModLoaded(string modId)
    {
        if (_instance.HasMethod("on_mod_loaded"))
            _instance.Call("on_mod_loaded", modId);
    }

    /// <summary>Calls <c>on_mod_resources_ready()</c> on the script when it defines it.</summary>
    public void OnModResourcesReady()
    {
        if (_instance.HasMethod("on_mod_resources_ready"))
            _instance.Call("on_mod_resources_ready");
    }
}
