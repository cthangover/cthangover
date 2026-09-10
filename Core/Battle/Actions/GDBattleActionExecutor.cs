using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.Battle.Actions
{
	public class GDBattleActionExecutor : IActionExecutor
	{
		private readonly GodotObject _instance;

		public GDBattleActionExecutor(string actionId, GodotObject instance)
		{
			ActionId = actionId;
			_instance = instance;
		}

		public string ActionId { get; }

		public ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
		{
			var gdAction = GDActionCharacter.From(action);
			var gdUser = GDCharacter.From(user);
			var gdTarget = GDCharacter.From(target);

			var variant = _instance.Call("execute", gdAction, gdUser, gdTarget);
			var result = variant.AsGodotObject() as GDBattleResult;
			return result?.ToChangedAttributes() ?? new ChangedAttributes { Result = false };
		}
	}
}
