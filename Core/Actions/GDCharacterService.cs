using Godot;

namespace Cthangover.Core.Actions
{
	[GlobalClass]
	public partial class GDCharacterService : RefCounted
	{
		internal ICharacterService Inner;

		public void AddToParty(string type) => Inner.AddToParty(type);
		public void SendNotification(string type) => Inner.SendNotification(type);
		public void AddSkill(string id) => Inner.AddSkill(id);
	}
}
