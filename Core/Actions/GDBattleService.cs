using Godot;

namespace Cthangover.Core.Actions
{
	[GlobalClass]
	public partial class GDBattleService : RefCounted
	{
		internal IBattleService Inner;

		public void Init(string sceneType, string enemies, string questId, string newTag)
			=> Inner.Init(
				sceneType,
				enemies,
				string.IsNullOrEmpty(questId) ? null : questId,
				string.IsNullOrEmpty(newTag) ? null : newTag);
	}
}
