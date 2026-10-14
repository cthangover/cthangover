using Godot;

namespace Cthangover.Core.Actions
{
	/// <summary>
	/// GDScript-facing quest subsystem exposed on the action context
	/// (<c>ctx.Quests</c>). Thin delegate over <see cref="IQuestService"/>; calls
	/// forward to the C# context bound by <see cref="GDActionContextCore.Inner"/>,
	/// so missing quests are skipped safely exactly as in C# actions.
	/// </summary>
	[GlobalClass]
	public partial class GDQuestService : RefCounted
	{
		internal IQuestService Inner;

		/// <summary>GDScript: ctx.Quests.Exists("q") — true when the quest is registered.</summary>
		public bool Exists(string id) => Inner.Exists(id);
		/// <summary>GDScript: ctx.Quests.SetStatus("q", "Progress") — sets the lifecycle state from a case-insensitive QuestStatus enum name (NotStarted / Progress / End).</summary>
		public void SetStatus(string id, string status) => Inner.SetStatus(id, status);
		/// <summary>GDScript: ctx.Quests.SetDataStatus("q", 3) — sets the quest's numeric progress counter.</summary>
		public void SetDataStatus(string id, int level) => Inner.SetDataStatus(id, level);
		/// <summary>GDScript: ctx.Quests.AddTag("q", "met_marao") — attaches a condition tag.</summary>
		public void AddTag(string id, string tag) => Inner.AddTag(id, tag);
		/// <summary>GDScript: ctx.Quests.RemoveTag("q", "met_marao") — removes a condition tag.</summary>
		public void RemoveTag(string id, string tag) => Inner.RemoveTag(id, tag);
		/// <summary>GDScript: ctx.Quests.SendNotification("q") — shows the "Quest Updated" popup without changing state.</summary>
		public void SendNotification(string id) => Inner.SendNotification(id);
	}
}
