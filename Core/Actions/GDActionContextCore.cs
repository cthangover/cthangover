namespace Cthangover.Core.Actions;

/// <summary>
/// Shared backing state for the GDScript-facing context wrappers. Holds the
/// six GD service singletons and the C# <see cref="IActionContext"/> they
/// delegate to. Assigning <see cref="Inner"/> fans the context out to every
/// service at once, so <see cref="GDScriptScenarioAction"/> can bind a freshly
/// created <see cref="GDActionContext"/> to the running dialog context with a
/// single property set. All members are null-safe: before binding, calls
/// silently no-op instead of throwing inside GDScript.
/// </summary>
public class GDActionContextCore
{
	/// <summary>
	/// The live C# context of the running scenario action. Setting it
	/// propagates the underlying services into every GD wrapper field.
	/// Null while the context is not bound to a running action.
	/// </summary>
	public IActionContext Inner
	{
		get => _inner;
		set
		{
			_inner = value;
			Quests.Inner = value?.Quests;
			Character.Inner = value?.Character;
			Lighting.Inner = value?.Lighting;
			Scene.Inner = value?.Scene;
			Inventory.Inner = value?.Inventory;
			ModRegistry.Inner = value?.ModRegistry;
		}
	}

	/// <summary>Delegates to the bound context; returns null before binding.</summary>
	public string GetParam(string name) => _inner?.GetParam(name);
	/// <summary>Delegates to the bound context; no-op before binding.</summary>
	public void Log(string category, string message) => _inner?.Log(category, message);
	/// <summary>Delegates to the bound context; no-op before binding.</summary>
	public void LogWarning(string category, string message) => _inner?.LogWarning(category, message);

	/// <summary>Quest subsystem wrapper bound by <see cref="Inner"/>.</summary>
	public readonly GDQuestService Quests = new();
	/// <summary>Character subsystem wrapper bound by <see cref="Inner"/>.</summary>
	public readonly GDCharacterService Character = new();
	/// <summary>Lighting subsystem wrapper bound by <see cref="Inner"/>.</summary>
	public readonly GDLightingService Lighting = new();
	/// <summary>Scene node subsystem wrapper bound by <see cref="Inner"/>.</summary>
	public readonly GDSceneNodeService Scene = new();
	/// <summary>Inventory subsystem wrapper bound by <see cref="Inner"/>.</summary>
	public readonly Items.GDInventoryService Inventory = new();
	/// <summary>Mod registry wrapper bound by <see cref="Inner"/>.</summary>
	public readonly Mods.GDModRegistryService ModRegistry = new();

	private IActionContext _inner;
}
