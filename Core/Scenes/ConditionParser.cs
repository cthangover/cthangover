using System;
using System.Collections.Generic;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Tokenizes condition expression strings and delegates tree construction
    /// to the pluggable <see cref="Parsing.ExpressionParser"/>. Operators are
    /// resolved through <see cref="Parsing.OperatorRegistry"/>; leaf values
    /// through <see cref="Parsing.LeafParserRegistry"/>. Mods can register
    /// custom parsers in either registry to extend the condition DSL without
    /// touching core code.
    /// </summary>
    public static class ConditionParser
    {
        public enum TokenType
        {
            Ident, Number, String, Dot, LParen, RParen, Comma,
            And, Or, Not,
            Eq, Neq, Ge, Le, Gt, Lt,
            EOF
        }

        public class Token
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

        /// <summary>
        /// Parses a condition expression string into a <see cref="ConditionNode"/> AST.
        /// Delegates tokenisation to the built-in tokenizer and tree construction to
        /// the pluggable <see cref="Parsing.ExpressionParser"/> which resolves operators
        /// through <see cref="Parsing.OperatorRegistry"/> and leaf values through
        /// <see cref="Parsing.LeafParserRegistry"/>.
        ///
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
            return (ConditionNode)new Parsing.ExpressionParser().Parse(tokens);
        }
    }
}
