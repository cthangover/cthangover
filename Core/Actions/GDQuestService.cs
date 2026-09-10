using Godot;

namespace Cthangover.Core.Actions
{
	[GlobalClass]
	public partial class GDQuestService : RefCounted
	{
		internal IQuestService Inner;

		public bool Exists(string id) => Inner.Exists(id);
		public void SetStatus(string id, string status) => Inner.SetStatus(id, status);
		public void SetDataStatus(string id, int level) => Inner.SetDataStatus(id, level);
		public void AddTag(string id, string tag) => Inner.AddTag(id, tag);
		public void RemoveTag(string id, string tag) => Inner.RemoveTag(id, tag);
		public void SendNotification(string id) => Inner.SendNotification(id);
	}
}
