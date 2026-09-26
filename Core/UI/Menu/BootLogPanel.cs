using System.Collections.Generic;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Terminal-style boot log feed shown by the launcher while mods load on a
    /// background thread. Renders the most recent line (the "active" one) plus up
    /// to four older lines, each fading out smoothly over time. Log entries arrive
    /// from the worker thread via <see cref="GameLogger.LogEmitted"/>, so the
    /// handler only appends to a lock-protected queue; the actual Godot label
    /// updates happen on the main thread inside <see cref="_Process"/>.
    /// </summary>
    public partial class BootLogPanel : Control
    {
        private const int VisibleLines = 5;
        private const float FadeSeconds = 3.0f;

        private readonly Queue<LogLine> _pending = new();
        private readonly object _queueLock = new();
        private readonly List<LogLine> _lines = new();

        private Label[] _labels;

        private struct LogLine
        {
            public string Text;
            public float Age;
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            var box = new VBoxContainer();
            box.SetAnchorsPreset(LayoutPreset.FullRect);
            box.Alignment = BoxContainer.AlignmentMode.End;
            box.AddThemeConstantOverride("separation", 2);
            AddChild(box);

            _labels = new Label[VisibleLines];
            for (var i = 0; i < VisibleLines; i++)
            {
                var label = new Label();
                label.AddThemeFontSizeOverride("font_size", 15);
                label.AddThemeColorOverride("font_color", UiTokens.Body);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.Modulate = UiTokens.Alpha(UiTokens.White, 0f);
                box.AddChild(label);
                _labels[i] = label;
            }

            foreach (var line in GameLogger.GetRecentLines(VisibleLines))
            {
                _lines.Add(new LogLine { Text = line, Age = 0f });
            }

            GameLogger.LogEmitted += OnLogEmitted;
        }

        public override void _ExitTree()
        {
            GameLogger.LogEmitted -= OnLogEmitted;
        }

        private void OnLogEmitted(string category, string message, LogLevel level)
        {
            lock (_queueLock)
            {
                _pending.Enqueue(new LogLine { Text = $"[{category}] {message}", Age = 0f });
            }
        }

        public override void _Process(double delta)
        {
            lock (_queueLock)
            {
                while (_pending.Count > 0)
                    _lines.Add(_pending.Dequeue());
            }

            // Age every line; drop ones that have faded completely.
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                var line = _lines[i];
                line.Age += (float)delta;
                _lines[i] = line;
                if (line.Age >= FadeSeconds)
                    _lines.RemoveAt(i);
            }

            // Cap history so a fast log burst (e.g. mod discovery) cannot grow the
            // list without bound before lines age out.
            while (_lines.Count > 256)
                _lines.RemoveAt(0);

            // Show the newest line + up to 4 before it; older = more transparent.
            // The newest line sits in the bottom-most label (index VisibleLines-1),
            // so with few lines the top labels stay empty.
            var shown = System.Math.Min(_lines.Count, VisibleLines);
            var start = _lines.Count - shown;

            for (var i = 0; i < VisibleLines; i++)
            {
                var labelIndex = VisibleLines - shown + i;
                if (i < shown)
                {
                    var line = _lines[start + i];
                    var alpha = Mathf.Clamp(1f - line.Age / FadeSeconds, 0f, 1f);
                    _labels[labelIndex].Text = line.Text;
                    _labels[labelIndex].Modulate = UiTokens.Alpha(UiTokens.White, alpha);
                }
                else
                {
                    _labels[labelIndex].Text = "";
                    _labels[labelIndex].Modulate = UiTokens.Alpha(UiTokens.White, 0f);
                }
            }
        }
    }
}
