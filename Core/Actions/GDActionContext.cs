using Cthangover.Core.Events;
using Godot;

namespace Cthangover.Core.Actions;

/// <summary>
/// GDScript-facing action context passed to <c>run(ctx)</c> of GDScript
/// scenario actions (see <see cref="GDScriptScenarioAction"/>). Mirrors the
/// <see cref="IActionContext"/> surface with snake-callable wrappers: subsystem
/// services (<see cref="Quests"/>, <see cref="Character"/>, <see cref="Lighting"/>,
/// <see cref="Scene"/>, <see cref="Inventory"/>, <see cref="ModRegistry"/>),
/// parameter access (<see cref="GetParam"/>), logging, and event publishing.
/// The backing C# context is attached just before the call via
/// <see cref="GDActionContextCore.Inner"/>, so a GDScript action sees exactly
/// the state a C# action would.
/// </summary>
[GlobalClass]
public partial class GDActionContext : RefCounted
{
	internal GDActionContextCore Core = new();

	/// <summary>Quest subsystem: status, tags, and UI notifications.</summary>
	public GDQuestService Quests => Core.Quests;
	/// <summary>Character subsystem: party recruitment and join notifications.</summary>
	public GDCharacterService Character => Core.Character;
	/// <summary>Lighting subsystem: depth/albedo maps and time-of-day lighting.</summary>
	public GDLightingService Lighting => Core.Lighting;
	/// <summary>Scene node subsystem: instantiate, remove, and find nodes.</summary>
	public GDSceneNodeService Scene => Core.Scene;
	/// <summary>Player inventory subsystem.</summary>
	public Items.GDInventoryService Inventory => Core.Inventory;
	/// <summary>Mod registry: query loaded mods and their metadata.</summary>
	public Mods.GDModRegistryService ModRegistry => Core.ModRegistry;

	/// <summary>Reads a named dialog variable (GDScript: <c>ctx.GetParam("quest_id")</c>). Returns null when unset.</summary>
	public string GetParam(string name) => Core.GetParam(name);
	/// <summary>Emits a log entry to the central game log under the given category.</summary>
	public void Log(string category, string message) => Core.Log(category, message);
	/// <summary>Emits a warning-level log entry (prefixed with "[WARN]").</summary>
	public void LogWarning(string category, string message) => Core.LogWarning(category, message);

	/// <summary>
	/// GDScript: ctx.PublishEvent("cooking_meal_ready", {"dish": "wolf_stew"})
	/// Publishes an event to the global bus for inter-mod communication.
	/// </summary>
	public void PublishEvent(string eventKey, Godot.Collections.Dictionary data)
	{
		ModEventBus.Instance?.Publish(eventKey, data);
	}
}
