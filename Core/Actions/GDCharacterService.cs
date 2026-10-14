using Godot;

namespace Cthangover.Core.Actions
{
	/// <summary>
	/// GDScript-facing character subsystem exposed on the action context
	/// (<c>ctx.Character</c>). Thin delegate over <see cref="ICharacterService"/>;
	/// every call forwards to the C# context bound by
	/// <see cref="GDActionContextCore.Inner"/>, so GDScript actions get the same
	/// behavior (and the same safe handling of unknown IDs) as C# actions.
	/// </summary>
	[GlobalClass]
	public partial class GDCharacterService : RefCounted
	{
		internal ICharacterService Inner;

		/// <summary>GDScript: ctx.Character.AddToParty("Murakami") — adds a character to the player's party by ID.</summary>
		public void AddToParty(string type) => Inner.AddToParty(type);
		/// <summary>GDScript: ctx.Character.SendNotification("Murakami") — shows the "character joined" popup without touching the roster.</summary>
		public void SendNotification(string type) => Inner.SendNotification(type);
		/// <summary>GDScript: ctx.Character.AddSkill("slave_master") — grants a skill and its action card to the player.</summary>
		public void AddSkill(string id) => Inner.AddSkill(id);
	}
}
