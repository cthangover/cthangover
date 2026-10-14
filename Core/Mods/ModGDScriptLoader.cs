using System;
using System.Collections.Generic;
using Cthangover.Core.Actions;
using Cthangover.Core.Events;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes.Parsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods;

/// <summary>
/// Loads and registers every <c>.gd</c> file declared by a mod's
/// <c>gd_sources</c> manifest patterns. Each file is compiled through Godot's
/// GDScript runtime (<c>GDScript.Reload</c>), instantiated once, and inspected
/// for known method patterns: <c>on_mod_event_*</c> handlers subscribe to the
/// string event bus, registered <see cref="IGDScriptHook"/>s get a chance to
/// claim subsystem-specific contracts (battle executors, winground listeners),
/// and the built-in patterns register scenario actions
/// (<c>get_name</c>+<c>run</c>), custom conditions
/// (<c>get_condition_name</c>+<c>evaluate</c>, inserted at the front of the
/// leaf-parser list), item actions (<c>get_item_action_id</c>+<c>use</c>) and
/// mod initializers (<c>on_mod_loaded</c>/<c>on_mod_resources_ready</c>).
/// One script can implement several patterns at once.
/// </summary>
public static class ModGDScriptLoader
{
	/// <summary>
	/// Walks every mod's <c>gd_sources</c> patterns, compiles each matching
	/// <c>.gd</c> file, and auto-registers it into the subsystems whose method
	/// patterns it implements. Pattern matching is always recursive: the part of
	/// a pattern before the last <c>/</c> selects the root directory and the
	/// suffix only filters file names.
	/// </summary>
	/// <param name="mods">The finalized mod set (id → info) to process.</param>
	public static void LoadGDScripts(IDictionary<string, IModInfo> mods)
	{
		foreach (var kvp in mods)
		{
			var modInfo = kvp.Value as ModInfo;
			var gdSources = modInfo?.Manifest?.GdSources;
			if (gdSources == null || gdSources.Count == 0)
				continue;

			foreach (var pattern in gdSources)
			{
				CollectGdFiles(modInfo.FileProvider, pattern, (filePath, source) =>
				{
					GameLogger.Log("GDS_MOD", $"Loading GDScript '{filePath}' from mod '{modInfo.Id}'");

					var gds = new GDScript { SourceCode = source };
					if (gds.Reload() != Error.Ok)
					{
						GameLogger.Log("GDS_MOD",
							$"Failed to compile GDScript '{filePath}' in mod '{modInfo.Id}'",
							LogLevel.Error);
						return;
					}

					var instance = gds.New();
					var obj = instance.AsGodotObject();
					if (obj == null)
					{
						GameLogger.Log("GDS_MOD",
							$"GDScript '{filePath}' in mod '{modInfo.Id}' did not produce a GodotObject",
							LogLevel.Error);
						return;
					}

					AutoRegisterEventHandlers(obj, modInfo.Id);

					GDScriptHookRegistry.TryRegisterAll(obj, modInfo.Id, filePath);

					if (obj.HasMethod("get_name") && obj.HasMethod("run"))
					{
						var name = obj.Call("get_name").AsString();
						if (!string.IsNullOrEmpty(name))
						{
							var action = new GDScriptScenarioAction(name, obj);
							ScenarioActionFactory.Instance.Register(action);
							GameLogger.Log("GDS_MOD",
								$"Registered GDScript action '{name}' from mod '{modInfo.Id}'");
						}
					}

					if (obj.HasMethod("get_condition_name") && obj.HasMethod("evaluate"))
					{
						var condName = obj.Call("get_condition_name").AsString();
						if (!string.IsNullOrEmpty(condName))
						{
							var leafParser = new GDLeafParser(condName, obj);
							LeafParserRegistry.RegisterFirst(leafParser);
							GameLogger.Log("GDS_MOD",
								$"Registered GDScript condition '{condName}' from mod '{modInfo.Id}'");
						}
					}

					if (obj.HasMethod("get_item_action_id") && obj.HasMethod("use"))
					{
						var itemActionId = obj.Call("get_item_action_id").AsString();
						if (!string.IsNullOrEmpty(itemActionId))
						{
							ItemActionBridge.Register(new GDItemAction(itemActionId, obj));
							GameLogger.Log("GDS_MOD",
								$"Registered GDScript item action '{itemActionId}' from mod '{modInfo.Id}'");
						}
					}

					if (obj.HasMethod("on_mod_loaded") || obj.HasMethod("on_mod_resources_ready"))
					{
						var initializer = new GDScriptModInitializer(obj);
						ModInitializerRegistry.RegisterGDScript(initializer, kvp.Key);
						GameLogger.Log("GDS_MOD",
							$"Registered GDScript initializer for mod '{modInfo.Id}'");
					}
				});
			}
		}
	}

    private static void CollectGdFiles(
        IModFileProvider provider,
        string pattern,
        Action<string, string> onFile)
    {
        var prefix = pattern;
        var suffix = "*.gd";
        var sepIndex = pattern.LastIndexOf('/');
        if (sepIndex >= 0)
        {
            prefix = pattern.Substring(0, sepIndex);
            suffix = pattern.Substring(sepIndex + 1);
        }

        CollectGdFilesRecursive(provider, prefix, file =>
        {
            if (!file.EndsWith(".gd", StringComparison.OrdinalIgnoreCase))
                return false;

            var fileName = file.Replace('\\', '/');
            var fileNamePart = fileName.Substring(fileName.LastIndexOf('/') + 1);

            return suffix == "*" || suffix == "*.gd" ||
                   (suffix.StartsWith("*.") &&
                    fileNamePart.EndsWith(suffix.Substring(1), StringComparison.OrdinalIgnoreCase));
        }, onFile);
    }

    private static void CollectGdFilesRecursive(
        IModFileProvider provider,
        string directory,
        Func<string, bool> fileFilter,
        Action<string, string> onFile)
    {
        foreach (var entry in provider.ListFiles(directory))
        {
            if (entry.EndsWith("/"))
            {
                var subDir = entry.TrimEnd('/');
                CollectGdFilesRecursive(provider, subDir, fileFilter, onFile);
            }
            else if (fileFilter(entry))
            {
                var source = provider.ReadFileText(entry);
                if (source != null)
                    onFile(entry, source);
            }
        }
    }

    private static void AutoRegisterEventHandlers(GodotObject obj, string modId)
    {
        var methods = obj.GetMethodList();
        foreach (var methodDict in methods)
        {
            var methodName = methodDict["name"].AsString();
            if (!methodName.StartsWith("on_mod_event_"))
                continue;

            var eventKey = methodName.Substring("on_mod_event_".Length);
            var bus = ModEventBus.Instance;
            if (bus == null)
                continue;

            bus.Subscribe(eventKey, new Godot.Callable(obj, methodName), modId: modId);
            GameLogger.Log("GDS_MOD",
                $"Auto-registered event handler '{eventKey}' -> {methodName} from mod '{modId}'");
        }
    }
}
