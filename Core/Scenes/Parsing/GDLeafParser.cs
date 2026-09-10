using System;
using Godot;

namespace Cthangover.Core.Scenes.Parsing
{
	public class GDLeafParser : ILeafParser
	{
		private readonly string _conditionName;
		private readonly GodotObject _instance;

		public GDLeafParser(string conditionName, GodotObject instance)
		{
			_conditionName = conditionName;
			_instance = instance;
		}

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
