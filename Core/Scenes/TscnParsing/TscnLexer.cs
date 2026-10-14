using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Categories of tokens produced by <see cref="TscnLexer"/>.
    /// <see cref="SectionHeader"/> — a line like <c>[node name="Root"]</c>;
    /// <see cref="Property"/> — a key=value line under a section;
    /// <see cref="Empty"/> — a blank line (unused by parser, preserved for diagnostics).
    /// </summary>
    public enum TscnTokenType { SectionHeader, Property, Empty }

    /// <summary>
    /// Single token produced by <see cref="TscnLexer"/>.
    /// For section headers, <see cref="SectionName"/> and <see cref="SectionAttrs"/> are populated;
    /// for properties, <see cref="PropertyKey"/> and <see cref="PropertyValue"/>.
    /// <see cref="LineNumber"/> tracks the original source line for error reporting.
    /// </summary>
    public class TscnToken
    {
        /// <summary>Token category: section header, property line, or empty line.</summary>
        public TscnTokenType Type { get; set; }
        /// <summary>Section name without brackets for <see cref="TscnTokenType.SectionHeader"/> tokens (e.g. "node").</summary>
        public string SectionName { get; set; }
        /// <summary>Parsed key=value attributes of the section header (editor-only keys already stripped).</summary>
        public Dictionary<string, string> SectionAttrs { get; set; }
        /// <summary>Key (left of <c>=</c>) for <see cref="TscnTokenType.Property"/> tokens.</summary>
        public string PropertyKey { get; set; }
        /// <summary>Raw right-hand side for <see cref="TscnTokenType.Property"/> tokens; parsing is deferred to <see cref="TscnPropertyParser"/>.</summary>
        public string PropertyValue { get; set; }
        /// <summary>1-based source line, for diagnostics.</summary>
        public int LineNumber { get; set; }

        /// <summary>Factory for a [section_header] token with parsed key=value attributes.</summary>
        public static TscnToken Section(string name, Dictionary<string, string> attrs, int line)
            => new() { Type = TscnTokenType.SectionHeader, SectionName = name, SectionAttrs = attrs, LineNumber = line };

        /// <summary>Factory for a key = value property token.</summary>
        public static TscnToken Prop(string key, string value, int line)
            => new() { Type = TscnTokenType.Property, PropertyKey = key, PropertyValue = value, LineNumber = line };

        /// <summary>Factory for an empty line token (preserved for source position tracking).</summary>
        public static TscnToken EmptyLine(int line)
            => new() { Type = TscnTokenType.Empty, LineNumber = line };
    }

    /// <summary>
    /// Line-by-line tokenizer for .tscn files. Splits source text into a flat sequence of
    /// <see cref="TscnToken"/> objects — section headers and property lines. Empty lines and
    /// comment lines (starting with <c>;</c>) are skipped.
    /// <para/>
    /// Does not parse property values — that is deferred to <see cref="TscnPropertyParser"/>
    /// to keep the lexer simple and avoid creating Godot objects during tokenization.
    /// Section header attributes (key=value pairs in brackets) are parsed inline;
    /// editor-only attributes (<c>uid</c>, <c>load_steps</c>, <c>unique_id</c>) are stripped.
    /// </summary>
    public class TscnLexer
    {
        /// <summary>Tokenizes raw .tscn source text into a flat sequence of TscnToken objects.</summary>
        public List<TscnToken> Tokenize(string source)
        {
            var tokens = new List<TscnToken>();

            if (string.IsNullOrEmpty(source))
                return tokens;

            var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var line = raw.TrimStart();

                if (line.Length == 0 || line.StartsWith(";"))
                    continue;

                if (line.StartsWith("["))
                {
                    var token = ParseSectionHeader(line, i + 1);
                    if (token != null)
                        tokens.Add(token);
                    continue;
                }

                var eqIdx = line.IndexOf('=');
                if (eqIdx >= 0)
                {
                    var key = line.Substring(0, eqIdx).TrimEnd();
                    var value = line.Substring(eqIdx + 1).TrimStart();
                    tokens.Add(TscnToken.Prop(key, value, i + 1));
                }
            }

            return tokens;
        }

        private static TscnToken ParseSectionHeader(string line, int lineNumber)
        {
            var closeIdx = line.LastIndexOf(']');
            if (closeIdx < 0)
                return null;

            var inner = line.Substring(1, closeIdx - 1);
            var attrs = new Dictionary<string, string>();

            var spaceIdx = inner.IndexOf(' ');
            string sectionName;
            string remainder;

            if (spaceIdx < 0)
            {
                sectionName = inner;
                remainder = "";
            }
            else
            {
                sectionName = inner.Substring(0, spaceIdx);
                remainder = inner.Substring(spaceIdx + 1).TrimStart();
            }

            while (remainder.Length > 0)
            {
                var eqIdx = remainder.IndexOf('=');
                if (eqIdx < 0)
                    break;

                var key = remainder.Substring(0, eqIdx).TrimEnd();
                remainder = remainder.Substring(eqIdx + 1).TrimStart();

                if (remainder.Length == 0)
                    break;

                var value = ParseSectionValue(ref remainder);
                if (key.StartsWith("unique_id") || key == "load_steps" || key == "uid")
                {
                    key = null;
                }

                if (key != null)
                    attrs[key] = value;
            }

            return TscnToken.Section(sectionName, attrs, lineNumber);
        }

        private static string ParseSectionValue(ref string input)
        {
            if (input.StartsWith("\""))
                return ParseQuotedString(ref input);

            return ParseUnquotedToken(ref input);
        }

        private static string ParseQuotedString(ref string input)
        {
            var result = "";
            var i = 1;

            while (i < input.Length)
            {
                var ch = input[i];
                if (ch == '\\' && i + 1 < input.Length)
                {
                    result += input[i + 1];
                    i += 2;
                    continue;
                }
                if (ch == '"')
                {
                    i++;
                    break;
                }
                result += ch;
                i++;
            }

            input = input.Substring(i).TrimStart();
            return result;
        }

        private static string ParseUnquotedToken(ref string input)
        {
            var parenDepth = 0;
            var bracketDepth = 0;
            var inString = false;
            var i = 0;

            while (i < input.Length)
            {
                var ch = input[i];

                if (inString)
                {
                    if (ch == '\\' && i + 1 < input.Length) { i += 2; continue; }
                    if (ch == '"') inString = false;
                    i++;
                    continue;
                }

                if (ch == '"') { inString = true; i++; continue; }
                if (ch == '(') { parenDepth++; i++; continue; }
                if (ch == ')') { parenDepth--; i++; continue; }
                if (ch == '[') { bracketDepth++; i++; continue; }
                if (ch == ']') { bracketDepth--; i++; continue; }

                if (parenDepth == 0 && bracketDepth == 0 && (ch == ' ' || ch == ']'))
                    break;

                i++;
            }

            var token = input.Substring(0, i);
            input = input.Substring(i).TrimStart();
            return token;
        }
    }
}
