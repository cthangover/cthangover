using System;
using System.Collections.Generic;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Recursive-descent parser that converts condition expression strings into an
    /// abstract syntax tree of <see cref="ConditionNode"/> objects. Supports boolean
    /// logic (<c>&amp;&amp;</c>, <c>||</c>, <c>!</c>, grouping with parentheses), six
    /// comparison operators (<c>==</c>, <c>!=</c>, <c>&gt;=</c>, <c>&lt;=</c>,
    /// <c>&gt;</c>, <c>&lt;</c>), flag-to-literal comparisons, quest property comparisons
    /// (<c>quest.Status >= 3</c> or <c>quest.DataStatus == 0</c>), and quest tag checks
    /// (<c>quest.hasTag("tag")</c>, <c>quest.notHasTag("tag")</c>).
    /// </summary>
    public static class ConditionParser
    {
        private enum TokenType
        {
            Ident, Number, String, Dot, LParen, RParen, Comma,
            And, Or, Not,
            Eq, Neq, Ge, Le, Gt, Lt,
            EOF
        }

        private class Token
        {
            public TokenType Type;
            public string Value;
        }

        private class Tokenizer
        {
            private readonly string input;
            private int pos;
            private readonly List<Token> tokens = new();

            public Tokenizer(string input)
            {
                this.input = input;
            }

            public List<Token> Tokenize()
            {
                tokens.Clear();
                pos = 0;
                while (pos < input.Length)
                {
                    SkipWhitespace();
                    if (pos >= input.Length) break;

                    var c = input[pos];

                    if (c == '(') { Add(TokenType.LParen, "("); pos++; }
                    else if (c == ')') { Add(TokenType.RParen, ")"); pos++; }
                    else if (c == ',') { Add(TokenType.Comma, ","); pos++; }
                    else if (c == '.') { Add(TokenType.Dot, "."); pos++; }
                    else if (c == '!')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '=')
                        { Add(TokenType.Neq, "!="); pos += 2; }
                        else
                        { Add(TokenType.Not, "!"); pos++; }
                    }
                    else if (c == '=')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '=')
                        { Add(TokenType.Eq, "=="); pos += 2; }
                        else throw Error("expected '='");
                    }
                    else if (c == '>')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '=')
                        { Add(TokenType.Ge, ">="); pos += 2; }
                        else
                        { Add(TokenType.Gt, ">"); pos++; }
                    }
                    else if (c == '<')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '=')
                        { Add(TokenType.Le, "<="); pos += 2; }
                        else
                        { Add(TokenType.Lt, "<"); pos++; }
                    }
                    else if (c == '&')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '&')
                        { Add(TokenType.And, "&&"); pos += 2; }
                        else throw Error("expected '&'");
                    }
                    else if (c == '|')
                    {
                        if (pos + 1 < input.Length && input[pos + 1] == '|')
                        { Add(TokenType.Or, "||"); pos += 2; }
                        else throw Error("expected '|'");
                    }
                    else if (c == '"' || c == '\'')
                    {
                        var quote = c;
                        pos++;
                        var start = pos;
                        while (pos < input.Length && input[pos] != quote)
                            pos++;
                        if (pos >= input.Length) throw Error("unterminated string");
                        var val = input.Substring(start, pos - start);
                        Add(TokenType.String, val);
                        pos++;
                    }
                    else if (char.IsDigit(c))
                    {
                        var start = pos;
                        while (pos < input.Length && char.IsDigit(input[pos]))
                            pos++;
                        Add(TokenType.Number, input.Substring(start, pos - start));
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        var start = pos;
                        while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_'))
                            pos++;
                        Add(TokenType.Ident, input.Substring(start, pos - start));
                    }
                    else
                    {
                        throw Error($"unexpected character '{c}'");
                    }
                }
                Add(TokenType.EOF, "");
                return tokens;
            }

            private void SkipWhitespace()
            {
                while (pos < input.Length && char.IsWhiteSpace(input[pos]))
                    pos++;
            }

            private void Add(TokenType type, string value)
            {
                tokens.Add(new Token { Type = type, Value = value });
            }

            private FormatException Error(string msg)
            {
                return new FormatException($"Condition parse error at pos {pos}: {msg}\n{input}\n{new string(' ', pos)}^");
            }
        }

        private class Parser
        {
            private readonly List<Token> tokens;
            private int pos;

            public Parser(List<Token> tokens)
            {
                this.tokens = tokens;
                pos = 0;
            }

            private Token Peek => tokens[pos];
            private Token Advance() => tokens[pos++];
            private bool Check(TokenType type) => Peek.Type == type;
            private Token Expect(TokenType type, string msg)
            {
                if (Peek.Type != type)
                    throw new FormatException($"Condition parse error: {msg}, got '{Peek.Type}' ('{Peek.Value}')");
                return Advance();
            }

            public ConditionNode Parse()
            {
                var node = ParseOr();
                if (!Check(TokenType.EOF))
                    throw new FormatException($"Condition parse error: unexpected token '{Peek.Value}' at end of expression");
                return node;
            }

            private ConditionNode ParseOr()
            {
                var left = ParseAnd();
                while (Check(TokenType.Or))
                {
                    Advance();
                    var right = ParseAnd();
                    left = new BinaryExpr(left, "or", right);
                }
                return left;
            }

            private ConditionNode ParseAnd()
            {
                var left = ParseUnary();
                while (Check(TokenType.And))
                {
                    Advance();
                    var right = ParseUnary();
                    left = new BinaryExpr(left, "and", right);
                }
                return left;
            }

            private ConditionNode ParseUnary()
            {
                if (Check(TokenType.Not))
                {
                    Advance();
                    return new UnaryExpr("not", ParseUnary());
                }
                return ParsePrimary();
            }

            private ConditionNode ParsePrimary()
            {
                if (Check(TokenType.LParen))
                {
                    Advance();
                    var node = ParseOr();
                    Expect(TokenType.RParen, "expected ')' after expression");
                    return node;
                }

                if (Check(TokenType.Ident))
                {
                    var ident = Advance().Value;

                    if (Check(TokenType.Dot))
                    {
                        Advance();
                        return ParseQuestCondition(ident);
                    }

                    return ParseFlagCondition(ident);
                }

                throw new FormatException($"Condition parse error: unexpected token '{Peek.Value}'");
            }

            private ConditionNode ParseQuestCondition(string questId)
            {
                var methodToken = Expect(TokenType.Ident, "expected method or property name after '.'");
                var method = methodToken.Value;

                if (method == "hasTag" || method == "notHasTag")
                {
                    Expect(TokenType.LParen, "expected '('");
                    var tag = Expect(TokenType.String, "expected string argument");
                    Expect(TokenType.RParen, "expected ')'");
                    return new QuestMethodCondition(questId, method, tag.Value);
                }

                if (method == "Status" || method == "DataStatus")
                {
                    var op = ParseCompOp();
                    var num = Expect(TokenType.Number, "expected number after comparison");
                    return new QuestPropCondition(questId, method, op, int.Parse(num.Value));
                }

                throw new FormatException($"Condition parse error: unknown quest property/method '{method}'");
            }

            private string ParseCompOp()
            {
                if (Check(TokenType.Eq)) { Advance(); return "=="; }
                if (Check(TokenType.Neq)) { Advance(); return "!="; }
                if (Check(TokenType.Ge)) { Advance(); return ">="; }
                if (Check(TokenType.Le)) { Advance(); return "<="; }
                if (Check(TokenType.Gt)) { Advance(); return ">"; }
                if (Check(TokenType.Lt)) { Advance(); return "<"; }
                throw new FormatException($"Condition parse error: expected comparison operator, got '{Peek.Value}'");
            }

            private ConditionNode ParseFlagCondition(string key)
            {
                var op = ParseCompOp();
                Token valToken;
                if (Check(TokenType.String) || Check(TokenType.Number))
                    valToken = Advance();
                else
                    throw new FormatException($"Condition parse error: expected string or number after operator, got '{Peek.Value}'");
                return new FlagCondition(key, op, valToken.Value);
            }
        }

        /// <summary>
        /// Parses a condition expression string into a <see cref="ConditionNode"/> AST.
        /// Returns <c>null</c> for null, empty, or whitespace-only input. Throws
        /// <see cref="FormatException"/> on syntax errors with position information.
        /// </summary>
        /// <param name="input">The condition expression string to parse.</param>
        public static ConditionNode Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var tokenizer = new Tokenizer(input);
            var tokens = tokenizer.Tokenize();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }
}
