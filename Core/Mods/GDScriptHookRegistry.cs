using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Registry of <see cref="IGDScriptHook"/> implementations. Hooks are
    /// registered by mod initializers during assembly registration, which always
    /// happens before <see cref="ModGDScriptLoader"/> processes <c>.gd</c> sources.
    /// A later registration with the same <see cref="IGDScriptHook.Id"/> replaces
    /// the earlier one so mod reloads do not accumulate duplicates.
    /// </summary>
    public static class GDScriptHookRegistry
    {
        private static readonly List<IGDScriptHook> hooks = new();

        /// <summary>Registers (or replaces by id) a GDScript hook.</summary>
        public static void Register(IGDScriptHook hook)
        {
            if (hook == null || string.IsNullOrEmpty(hook.Id))
                return;

            hooks.RemoveAll(existing => existing.Id == hook.Id);
            hooks.Add(hook);
            GameLogger.Log("GDS_MOD", $"GDScriptHookRegistry: registered hook '{hook.Id}'");
        }

        /// <summary>Offers a compiled GDScript object to every registered hook.</summary>
        public static void TryRegisterAll(GodotObject obj, string modId, string filePath)
        {
            if (obj == null)
                return;

            foreach (var hook in hooks)
            {
                try
                {
                    hook.TryRegister(obj, modId, filePath);
                }
                catch (System.Exception ex)
                {
                    GameLogger.Log("GDS_MOD",
                        $"GDScriptHookRegistry: hook '{hook.Id}' failed on '{filePath}' from mod '{modId}': {ex.Message}",
                        LogLevel.Error);
                }
            }
        }
    }
}
