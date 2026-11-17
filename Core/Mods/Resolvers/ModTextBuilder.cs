namespace Cthangover.Core.Mods.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>Emits text and provenance together; nested trimming clips spans without changing their original source lines.</summary>
    internal sealed class ModTextBuilder
    {
        private readonly StringBuilder text = new();
        private readonly List<ModTextOrigin> origins = new();
        private readonly List<int> rootIncludeLines = new();

        internal string Text => text.ToString();

        internal ModTextOrigin[] Origins => origins.ToArray();

        internal int[] RootIncludeLines => rootIncludeLines.ToArray();

        internal void AddRootInclude(int line)
        {
            if (!rootIncludeLines.Contains(line))
            {
                rootIncludeLines.Add(line);
            }
        }

        internal int Append(string content, int start, int length, string path, int line, int rootLine, int rootIncludeOffset)
        {
            var end = start + length;
            while (start < end)
            {
                var newline = content.IndexOf('\n', start, end - start);
                var count = newline < 0 ? end - start : newline - start + 1;
                origins.Add(new ModTextOrigin(text.Length, count, ModPath.Normalize(path), line,
                    rootIncludeOffset == 0 ? line : rootLine, rootIncludeOffset));
                text.Append(content, start, count);
                start += count;
                if (newline >= 0)
                {
                    line++;
                }
            }

            return line;
        }

        internal void AppendTrimmed(ModTextBuilder fragment)
        {
            var value = fragment.Text;
            var left = 0;
            var right = value.Length;
            while (left < right && char.IsWhiteSpace(value[left]))
            {
                left++;
            }

            while (right > left && char.IsWhiteSpace(value[right - 1]))
            {
                right--;
            }

            var offset = text.Length;
            text.Append(value, left, right - left);
            foreach (var origin in fragment.origins)
            {
                var from = Math.Max(left, origin.Start);
                var to = Math.Min(right, origin.Start + origin.Length);
                if (to > from)
                {
                    origins.Add(new ModTextOrigin(offset + from - left, to - from, origin.Path, origin.Line,
                        origin.RootLine, origin.RootIncludeOffset));
                }
            }
        }
    }
}
