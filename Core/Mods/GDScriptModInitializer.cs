using Godot;

namespace Cthangover.Core.Mods;

public class GDScriptModInitializer : IModInitializer
{
    private readonly GodotObject _instance;

    public GDScriptModInitializer(GodotObject instance)
    {
        _instance = instance;
    }

    public void OnModLoaded(string modId)
    {
        if (_instance.HasMethod("on_mod_loaded"))
            _instance.Call("on_mod_loaded", modId);
    }

    public void OnModResourcesReady()
    {
        if (_instance.HasMethod("on_mod_resources_ready"))
            _instance.Call("on_mod_resources_ready");
    }
}
