using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Scenes.Parsing
{
	public class GDCondition : ConditionNode
	{
		private readonly GodotObject _instance;
		private readonly Godot.Collections.Dictionary _args;

		public GDCondition(GodotObject instance, Godot.Collections.Dictionary args)
		{
			_instance = instance;
			_args = args;
		}

		public override bool Evaluate(EvaluationContext ctx)
		{
			var gdFlags = new Godot.Collections.Dictionary();
			foreach (var kv in ctx.Flags)
				gdFlags[kv.Key] = kv.Value;

			return _instance.Call("evaluate", gdFlags, _args).AsBool();
		}
	}
}
