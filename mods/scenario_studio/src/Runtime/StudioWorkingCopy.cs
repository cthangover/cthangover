namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;

    /// <summary>
    /// Unapplied edits of one scenario: its text as lines plus which lines were
    /// added or changed since the copy was taken. Every dock edit (insert, edit
    /// a line's text, choose an answer's target, edit the whole text) goes here
    /// first and reaches the game only on Apply, so an author can prepare
    /// several changes and replay them at once. Every line remembers its number
    /// in the original text (0 for added lines): the running dialog's actions
    /// carry original numbers, so the debugger's marks and breakpoints are
    /// mapped through it while edits shift the displayed numbering.
    /// </summary>
    public sealed class StudioWorkingCopy
    {
        private readonly List<string> _lines;
        private readonly List<bool> _changed;
        private readonly List<int> _origin;

        public string ScenarioName { get; }

        public StudioWorkingCopy(string scenarioName, string text)
        {
            ScenarioName = scenarioName;
            _lines = new List<string>((text ?? string.Empty).Replace("\r\n", "\n").Split('\n'));
            _changed = new List<bool>(new bool[_lines.Count]);
            _origin = new List<int>(_lines.Count);
            for (var i = 0; i < _lines.Count; i++)
                _origin.Add(i + 1);
        }

        /// <summary>Original (1-based) number of the displayed line <paramref name="number"/>; 0 for an added line.</summary>
        public int OriginalNumber(int number) => number >= 1 && number <= _origin.Count ? _origin[number - 1] : 0;

        public string Text => string.Join("\n", _lines);

        public int LineCount => _lines.Count;

        /// <summary>Number of added or changed lines.</summary>
        public int PendingCount
        {
            get
            {
                var count = 0;
                foreach (var changed in _changed)
                {
                    if (changed)
                        count++;
                }
                return count;
            }
        }

        /// <summary>Whether the line at 0-based <paramref name="index"/> was added or changed.</summary>
        public bool IsChanged(int index) => index >= 0 && index < _changed.Count && _changed[index];

        public string Line(int index) => index >= 0 && index < _lines.Count ? _lines[index] : string.Empty;

        /// <summary>Inserts lines before 0-based <paramref name="index"/> (clamped to the text).</summary>
        public void Insert(int index, params string[] lines)
        {
            index = System.Math.Clamp(index, 0, _lines.Count);
            _lines.InsertRange(index, lines);
            _changed.InsertRange(index, NewFlags(lines.Length));
            _origin.InsertRange(index, new int[lines.Length]);
        }

        /// <summary>Appends lines at the end of the text.</summary>
        public void Append(params string[] lines) => Insert(_lines.Count, lines);

        public void Replace(int index, string line)
        {
            if (index < 0 || index >= _lines.Count || _lines[index] == line)
                return;
            _lines[index] = line;
            _changed[index] = true;
        }

        /// <summary>
        /// Replaces the whole text (the inline text editor). Lines that differ from
        /// the line at the same index before the edit are marked changed; this is a
        /// display hint, not a diff, so an inserted line marks the shifted tail too.
        /// </summary>
        public void ReplaceAll(string text)
        {
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var changed = i >= _lines.Count || _lines[i] != lines[i] || _changed[i];
                if (i < _lines.Count)
                {
                    _lines[i] = lines[i];
                    _changed[i] = changed;
                }
                else
                {
                    _lines.Add(lines[i]);
                    _changed.Add(true);
                    _origin.Add(0);
                }
            }

            if (_lines.Count > lines.Length)
            {
                _lines.RemoveRange(lines.Length, _lines.Count - lines.Length);
                _changed.RemoveRange(lines.Length, _changed.Count - lines.Length);
                _origin.RemoveRange(lines.Length, _origin.Count - lines.Length);
            }
        }

        /// <summary>0-based index of the label line <c>:name</c>, or -1.</summary>
        public int FindLabel(string name)
        {
            for (var i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Trim() == ":" + name)
                    return i;
            }
            return -1;
        }

        private static IEnumerable<bool> NewFlags(int count)
        {
            for (var i = 0; i < count; i++)
                yield return true;
        }
    }
}
