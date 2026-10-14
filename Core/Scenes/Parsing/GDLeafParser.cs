using System;
using Godot;

namespace Cthangover.Core.Scenes.Parsing
{
	/// <summary>
	/// Leaf parser for GDScript custom conditions such as
	/// <c>gds.is_rain</c> or <c>gds.flag_equals("victory")</c>. Created by
	/// <c>ModGDScriptLoader</c> and inserted at the front of the
	/// <see cref="LeafParserRegistry"/> list, so a GDScript condition wins over the
	/// built-in quest/flag parsers. Matches when the next two identifiers joined by
	/// a dot equal the script's <c>get_condition_name()</c> value.
	/// </summary>
	public class GDLeafParser : ILeafParser
	{
		private readonly string _conditionName;
		private readonly GodotObject _instance;

		/// <summary>Creates a parser bound to one GDScript condition name.</summary>
		/// <param name="conditionName">Value returned by the script's <c>get_condition_name()</c>, e.g. "gds.is_rain".</param>
		/// <param name="instance">Live GodotObject whose <c>evaluate(flags, args)</c> computes the result.</param>
		public GDLeafParser(string conditionName, GodotObject instance)
		{
			_conditionName = conditionName;
			_instance = instance;
		}

		/// <summary>True when the next <c>ident.ident</c> pair matches this parser's condition name (checked on a positional snapshot).</summary>
		public bool CanParse(TokenPeeker peeker)
		{
			if (peeker.AtEnd || peeker.Peek.Type != ConditionParser.TokenType.Ident)
				return false;

			var snapshot = peeker.Snapshot();
			var ident = snapshot.Advance().Value;

			if (snapshot.AtEnd || snapshot.Peek.Type != ConditionParser.TokenType.Dot)
				return false;

			snapshot.Advance();
			if (snapshot.AtEnd || snapshot.Peek.Type != ConditionParser.TokenType.Ident)
				return false;

			var method = snapshot.Advance().Value;
			return $"{ident}.{method}" == _conditionName;
		}

		/// <summary>Consumes the condition name and an optional parenthesised argument list, producing a <see cref="GDCondition"/>.</summary>
		public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
		{
			peeker.Advance(); // ident
			peeker.Expect(ConditionParser.TokenType.Dot, "expected '.' after mod prefix");
			peeker.Advance(); // method name

			var args = new Godot.Collections.Dictionary();

			if (!peeker.AtEnd && peeker.Peek.Type == ConditionParser.TokenType.LParen)
			{
				peeker.Advance(); // '('

				if (!peeker.AtEnd && peeker.Peek.Type != ConditionParser.TokenType.RParen)
				{
					var argIndex = 0;
					while (true)
					{
						if (peeker.AtEnd)
							throw new FormatException("Condition parse error: unexpected end of arguments");

						args[argIndex.ToString()] = peeker.Advance().Value;
						argIndex++;

						if (peeker.Check(ConditionParser.TokenType.Comma))
						{
							peeker.Advance();
							continue;
						}
						break;
					}
				}

				peeker.Expect(ConditionParser.TokenType.RParen, "expected ')'");
			}

			return new GDCondition(_instance, args);
		}
	}
}
