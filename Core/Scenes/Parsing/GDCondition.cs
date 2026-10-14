using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Scenes.Parsing
{
	/// <summary>
	/// Condition leaf node backed by a GDScript custom condition (registered by
	/// <c>ModGDScriptLoader</c> through <see cref="GDLeafParser"/>). Evaluation
	/// converts the flag dictionary to a Godot Dictionary and invokes the script's
	/// <c>evaluate(flags, args)</c> method, so the whole custom-condition contract
	/// stays in GDScript without C# glue per mod.
	/// </summary>
	public class GDCondition : ConditionNode
	{
		private readonly GodotObject _instance;
		private readonly Godot.Collections.Dictionary _args;

		/// <summary>Wraps a GDScript instance and the positional arguments parsed after the condition name.</summary>
		/// <param name="instance">Live GodotObject exposing <c>evaluate(flags, args)</c>.</param>
		/// <param name="args">Positional call arguments keyed by their string index ("0", "1", …).</param>
		public GDCondition(GodotObject instance, Godot.Collections.Dictionary args)
		{
			_instance = instance;
			_args = args;
		}

		/// <summary>Copies flags into a Godot Dictionary and calls the script's <c>evaluate</c> method.</summary>
		public override bool Evaluate(EvaluationContext ctx)
		{
			var gdFlags = new Godot.Collections.Dictionary();
			foreach (var kv in ctx.Flags)
				gdFlags[kv.Key] = kv.Value;

			return _instance.Call("evaluate", gdFlags, _args).AsBool();
		}
	}
}
