using Godot;

namespace Cthangover.Core.Actions
{
	/// <summary>
	/// GDScript-facing scene node subsystem exposed on the action context
	/// (<c>ctx.Scene</c>). Thin delegate over <see cref="ISceneNodeService"/>;
	/// calls forward to the C# context bound by
	/// <see cref="GDActionContextCore.Inner"/>.
	/// </summary>
	[GlobalClass]
	public partial class GDSceneNodeService : RefCounted
	{
		internal ISceneNodeService Inner;

		/// <summary>GDScript: ctx.Scene.Instantiate("res://scenes/ui/x.tscn", "X") — instantiates a PackedScene into the current scene tree.</summary>
		public void Instantiate(string scenePath, string nodeName) => Inner.Instantiate(scenePath, nodeName);
		/// <summary>GDScript: ctx.Scene.Remove("X") — removes a named node from the scene tree (no-op when absent).</summary>
		public void Remove(string nodeName) => Inner.Remove(nodeName);
	}
}
