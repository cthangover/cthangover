namespace Cthangover.Mods.Stats.Formulas
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Recursive-descent parser of the formula language. Precedence from loose to tight:
    /// <c>?:</c>, <c>||</c>, <c>&amp;&amp;</c>, comparisons, <c>+ -</c>, <c>* / %</c>,
    /// prefix <c>- + !</c>, <c>^</c> (right-associative), then numbers, names, calls and
    /// parentheses. A name may contain dots (<c>a.atk</c>, <c>hero.str</c>): the dot is
    /// part of the name, the scope decides what it means. Names and functions are case
    /// insensitive and stored lowercase, like stat ids.
    /// </summary>
    public class FormulaParser
    {
        private readonly string text;
        private int pos;

        private FormulaParser(string text)
        {
            this.text = text ?? string.Empty;
        }

        /// <summary>Parses <paramref name="formula"/>; throws <see cref="FormulaException"/> on a syntax error.</summary>
        public static FormulaNode Parse(string formula)
        {
            var parser = new FormulaParser(formula);
            parser.SkipSpace();
            if (parser.AtEnd)
                throw new FormulaException(parser.text, 0, "empty formula");
            var node = parser.ParseConditional();
            parser.SkipSpace();
            if (!parser.AtEnd)
                throw parser.Error($"unexpected '{parser.text[parser.pos]}'");
            return node;
        }

        private bool AtEnd => pos >= text.Length;

        private FormulaNode ParseConditional()
        {
            var condition = ParseOr();
            if (!Match("?"))
                return condition;
            var then = ParseConditional();
            Expect(":");
            var otherwise = ParseConditional();
            return new FormulaNode(FormulaNodeKind.Conditional, children: new[] { condition, then, otherwise });
        }

        private FormulaNode ParseOr()
        {
            var left = ParseAnd();
            while (Match("||"))
                left = Binary("||", left, ParseAnd());
            return left;
        }

        private FormulaNode ParseAnd()
        {
            var left = ParseComparison();
            while (Match("&&"))
                left = Binary("&&", left, ParseComparison());
            return left;
        }

        private FormulaNode ParseComparison()
        {
            var left = ParseAdditive();
            foreach (var op in new[] { "<=", ">=", "==", "!=", "<", ">" })
            {
                if (Match(op))
                    return Binary(op, left, ParseAdditive());
            }
            return left;
        }

        private FormulaNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                if (Match("+"))
                    left = Binary("+", left, ParseMultiplicative());
                else if (Match("-"))
                    left = Binary("-", left, ParseMultiplicative());
                else
                    return left;
            }
        }

        private FormulaNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match("*"))
                    left = Binary("*", left, ParseUnary());
                else if (Match("/"))
                    left = Binary("/", left, ParseUnary());
                else if (Match("%"))
                    left = Binary("%", left, ParseUnary());
                else
                    return left;
            }
        }

        private FormulaNode ParseUnary()
        {
            // "!=" is a comparison, never a negation followed by "=".
            if (Peek("!") && !Peek("!="))
            {
                Match("!");
                return new FormulaNode(FormulaNodeKind.Unary, op: "!", children: new[] { ParseUnary() });
            }
            if (Match("-"))
                return new FormulaNode(FormulaNodeKind.Unary, op: "-", children: new[] { ParseUnary() });
            if (Match("+"))
                return ParseUnary();
            return ParsePower();
        }

        private FormulaNode ParsePower()
        {
            var left = ParsePrimary();
            return Match("^") ? Binary("^", left, ParseUnary()) : left;
        }

        private FormulaNode ParsePrimary()
        {
            SkipSpace();
            if (AtEnd)
                throw Error("expected a number, a name or '('");

            var c = text[pos];
            if (Match("("))
            {
                var inner = ParseConditional();
                Expect(")");
                return inner;
            }
            if (char.IsDigit(c) || c == '.')
                return ParseNumber();
            if (char.IsLetter(c) || c == '_')
                return ParseName();
            throw Error($"unexpected '{c}'");
        }

        private FormulaNode ParseNumber()
        {
            var start = pos;
            while (!AtEnd && (char.IsDigit(text[pos]) || text[pos] == '.'))
                pos++;
            var literal = text.Substring(start, pos - start);
            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormulaException(text, start, $"bad number '{literal}'");
            return new FormulaNode(FormulaNodeKind.Number, value);
        }

        private FormulaNode ParseName()
        {
            var start = pos;
            while (!AtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_' || text[pos] == '.'))
                pos++;
            var name = text.Substring(start, pos - start).TrimEnd('.').ToLowerInvariant();
            if (!Match("("))
                return new FormulaNode(FormulaNodeKind.Variable, name: name);

            var args = new List<FormulaNode>();
            if (!Match(")"))
            {
                do
                    args.Add(ParseConditional());
                while (Match(","));
                Expect(")");
            }
            if (!FormulaFunctions.Arity.ContainsKey(name))
                throw new FormulaException(text, start, $"unknown function '{name}'");
            if (!FormulaFunctions.Accepts(name, args.Count))
                throw new FormulaException(text, start, $"'{name}' does not take {args.Count} argument(s)");
            return new FormulaNode(FormulaNodeKind.Call, name: name, children: args.ToArray());
        }

        private static FormulaNode Binary(string op, FormulaNode left, FormulaNode right)
            => new(FormulaNodeKind.Binary, op: op, children: new[] { left, right });

        private void SkipSpace()
        {
            while (!AtEnd && char.IsWhiteSpace(text[pos]))
                pos++;
        }

        private bool Peek(string token)
        {
            SkipSpace();
            return string.CompareOrdinal(text, pos, token, 0, token.Length) == 0;
        }

        private bool Match(string token)
        {
            if (!Peek(token))
                return false;
            pos += token.Length;
            return true;
        }

        private void Expect(string token)
        {
            if (!Match(token))
                throw Error($"expected '{token}'");
        }

        private FormulaException Error(string message) => new(text, pos, message);
    }
}
