using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Cthangover.LocaleAtlas
{
    /// <summary>
    /// Byte-level check of one locale file. <c>LocaleLoader</c> reads files
    /// with <c>File.ReadAllText</c>/<c>StreamReader</c>, which silently turn
    /// invalid UTF-8 into U+FFFD, so broken text is only visible in the raw
    /// bytes. Detects: non-UTF-8 encodings (UTF-16/32 with or without BOM,
    /// cp1251/latin-1 bytes), invalid sequences, the replacement character,
    /// control characters, and mojibake — UTF-8 Cyrillic that was decoded as
    /// cp1251 (<c>РїСЂРёРІРµС‚</c>) or latin-1/cp1252 (<c>ÐŸÑ€Ð¸</c>) and
    /// saved again. Also reports the BOM and line endings.
    /// </summary>
    public static class LocaleEncodingChecker
    {
        public sealed class Issue
        {
            public LocaleSeverity Severity { get; set; }
            public string Code { get; set; }
            public int Line { get; set; }
            public string Message { get; set; }
        }

        public sealed class Result
        {
            /// <summary>Decoded text (BOM stripped); best effort when the file is not valid UTF-8.</summary>
            public string Text { get; set; } = string.Empty;
            public string Encoding { get; set; } = "UTF-8";
            public string LineEndings { get; set; } = "none";
            public List<Issue> Issues { get; } = new();
        }

        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        // Second byte of a UTF-8 Cyrillic pair (0x80–0xBF) as cp1251 shows it,
        // after Р (0xD0) or С (0xD1). Characters that also follow Р/С in real
        // Russian text (ё, Ё, «, », dashes, quotes, nbsp) are left out.
        private static readonly Regex Cp1251Mojibake = new(
            "[РС][ЂЃ‚ѓ„†‡€‰Љ‹ЊЌЋЏђ™љ›њќћџЎўЈ¤Ґ¦§©Є¬\u00AD®Ї°±Ііґµ¶·№єјЅѕї]",
            RegexOptions.Compiled);

        // The same pair as latin-1/cp1252 shows it (Ð = 0xD0, Ñ = 0xD1), and
        // Â/Ã + continuation for double-encoded Latin-1 text.
        private static readonly Regex Latin1Mojibake = new(
            "[ÐÑÂÃ][\u0080-¿ŒœŠšŸŽžƒˆ˜–—‘’‚“”„†‡•…‰‹›€™]",
            RegexOptions.Compiled);

        private static readonly Regex LostCharacters = new(@"\?{3,}", RegexOptions.Compiled);

        public static Result Check(byte[] bytes)
        {
            var result = new Result();
            if (bytes == null || bytes.Length == 0)
            {
                result.Encoding = "empty";
                return result;
            }

            var offset = 0;
            if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00) || StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF))
            {
                result.Encoding = "UTF-32";
                Add(result, LocaleSeverity.Error, "encoding_utf32", 0, "file is UTF-32; locale files must be UTF-8");
                result.Text = DecodeOrEmpty(new UTF32Encoding(bytes[0] == 0x00, true), bytes, 4);
                result.LineEndings = LineEndings(result.Text);
                return result;
            }

            if (StartsWith(bytes, 0xFF, 0xFE) || StartsWith(bytes, 0xFE, 0xFF))
            {
                var bigEndian = bytes[0] == 0xFE;
                result.Encoding = bigEndian ? "UTF-16 BE" : "UTF-16 LE";
                Add(result, LocaleSeverity.Error, "encoding_utf16", 0, $"file is {result.Encoding}; the loader reads it, but locale files must be UTF-8 (tools, diffs and zip packaging expect it)");
                result.Text = DecodeOrEmpty(new UnicodeEncoding(bigEndian, true), bytes, 2);
                result.LineEndings = LineEndings(result.Text);
                CheckText(result);
                return result;
            }

            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
            {
                offset = 3;
                result.Encoding = "UTF-8 BOM";
                Add(result, LocaleSeverity.Warning, "utf8_bom", 1, "UTF-8 with BOM; the loader strips it, but the project convention is UTF-8 without BOM");
            }

            if (LooksLikeUtf16WithoutBom(bytes, offset))
            {
                result.Encoding = "UTF-16 (no BOM)";
                Add(result, LocaleSeverity.Error, "encoding_utf16_nobom", 0, "file looks like UTF-16 without BOM (NUL every other byte); the loader reads it as garbage");
                return result;
            }

            try
            {
                result.Text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                result.Encoding = "invalid";
                ReportInvalidLines(bytes, offset, result);
                result.Text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
            }

            result.LineEndings = LineEndings(result.Text);
            if (result.LineEndings == "mixed")
                Add(result, LocaleSeverity.Warning, "mixed_line_endings", 0, "mixed LF/CRLF line endings");
            else if (result.LineEndings == "CR")
                Add(result, LocaleSeverity.Error, "cr_line_endings", 0, "old Mac CR-only line endings; the loader sees the whole file as one line");

            CheckText(result);
            return result;
        }

        /// <summary>Character-level checks on decoded text, line by line.</summary>
        private static void CheckText(Result result)
        {
            var lines = result.Text.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var number = i + 1;
                if (line.IndexOf('�') >= 0)
                    Add(result, LocaleSeverity.Error, "replacement_char", number, "contains U+FFFD (�): text was already broken by a wrong decode before it was saved");

                foreach (var c in line)
                {
                    if ((c < 0x20 && c != '\t' && c != '\r') || c == 0x7F || (c >= 0x80 && c < 0xA0))
                    {
                        Add(result, LocaleSeverity.Error, "control_char", number, $"control character U+{(int)c:X4}");
                        break;
                    }
                }

                if (line.Length > 0 && line[0] == '﻿' && !(i == 0 && result.Encoding == "UTF-8 BOM"))
                    Add(result, LocaleSeverity.Warning, "inner_bom", number, "U+FEFF inside the file (a BOM glued in by concatenation); it becomes part of the key");

                var cp1251 = Cp1251Mojibake.Match(line);
                if (cp1251.Success)
                    Add(result, LocaleSeverity.Error, "mojibake_cp1251", number, $"mojibake: UTF-8 text decoded as cp1251 near '{Excerpt(line, cp1251.Index)}'");

                var latin1 = Latin1Mojibake.Match(line);
                if (latin1.Success)
                    Add(result, LocaleSeverity.Error, "mojibake_latin1", number, $"mojibake: UTF-8 text decoded as latin-1/cp1252 near '{Excerpt(line, latin1.Index)}'");

                if (LostCharacters.IsMatch(line) && !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    Add(result, LocaleSeverity.Warning, "question_marks", number, "'???' in a value: characters may have been lost by saving in a non-Unicode code page");
            }
        }

        private static void ReportInvalidLines(byte[] bytes, int offset, Result result)
        {
            var line = 1;
            var start = offset;
            var reported = 0;
            for (var i = offset; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                try
                {
                    StrictUtf8.GetString(bytes, start, i - start);
                }
                catch (DecoderFallbackException)
                {
                    if (reported++ < 20)
                        Add(result, LocaleSeverity.Error, "invalid_utf8", line, $"invalid UTF-8 bytes{GuessLegacy(bytes, start, i - start)}");
                }

                line++;
                start = i + 1;
            }

            if (reported > 20)
                Add(result, LocaleSeverity.Error, "invalid_utf8", 0, $"… and {reported - 20} more line(s) with invalid UTF-8");
        }

        /// <summary>Hints the legacy code page: Cyrillic letters of cp1251 are bytes 0xC0–0xFF.</summary>
        private static string GuessLegacy(byte[] bytes, int start, int length)
        {
            var high = 0;
            var cyrillic = 0;
            for (var i = start; i < start + length; i++)
            {
                if (bytes[i] < 0x80)
                    continue;
                high++;
                if (bytes[i] >= 0xC0)
                    cyrillic++;
            }

            return high > 0 && cyrillic * 10 >= high * 7 ? " (looks like cp1251: re-save the file as UTF-8)" : string.Empty;
        }

        private static bool LooksLikeUtf16WithoutBom(byte[] bytes, int offset)
        {
            var length = Math.Min(bytes.Length - offset, 512);
            if (length < 4)
                return false;
            var nul = 0;
            for (var i = offset; i < offset + length; i++)
            {
                if (bytes[i] == 0)
                    nul++;
            }

            return nul * 4 >= length;
        }

        private static string LineEndings(string text)
        {
            var crlf = 0;
            var lf = 0;
            var cr = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        crlf++;
                        i++;
                    }
                    else
                        cr++;
                }
                else if (text[i] == '\n')
                    lf++;
            }

            var kinds = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
            if (kinds == 0)
                return "none";
            if (kinds > 1)
                return "mixed";
            return crlf > 0 ? "CRLF" : lf > 0 ? "LF" : "CR";
        }

        private static string DecodeOrEmpty(Encoding encoding, byte[] bytes, int offset)
        {
            try
            {
                return encoding.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                return string.Empty;
            }
        }

        private static string Excerpt(string line, int index)
        {
            var start = Math.Max(0, index - 4);
            return line.Substring(start, Math.Min(16, line.Length - start));
        }

        private static bool StartsWith(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length)
                return false;
            for (var i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i])
                    return false;
            }

            return true;
        }

        private static void Add(Result result, LocaleSeverity severity, string code, int line, string message)
            => result.Issues.Add(new Issue { Severity = severity, Code = code, Line = line, Message = message });
    }
}
