namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Godot;

    /// <summary>
    /// One script line of the author mode (<c>.ct-code__line</c>): gutter with
    /// the breakpoint dot, line number and the ▶ marker of the current line,
    /// then the coloured source. A click on the gutter toggles the breakpoint,
    /// like in an IDE; a parse error is shown as a faint comment after the
    /// source so the line keeps its place. The source label has a fixed line
    /// height instead of fit-content: a fitted label without wrapping demands its
    /// full text width and would widen the whole dock for one long line.
    /// </summary>
    public partial class StudioCodeLineView : PanelContainer
    {
        private const int GutterWidth = 58;
        private const int LineHeight = 20;

        private readonly StudioCodeLine _line;
        private readonly Control _gutter;

        /// <summary>The line this row shows.</summary>
        public StudioCodeLine Line => _line;

        /// <summary>The clickable gutter (breakpoint dot, number, marker).</summary>
        public Control Gutter => _gutter;

        /// <summary>Raised when the gutter is clicked.</summary>
        public event Action<StudioCodeLine> BreakpointToggled;

        public StudioCodeLineView(StudioCodeLine line)
        {
            _line = line;
            MouseFilter = MouseFilterEnum.Pass;
            AddThemeStyleboxOverride("panel", StudioStyles.CodeLine(line));

            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 0);
            AddChild(row);

            _gutter = new HBoxContainer { CustomMinimumSize = new Vector2(GutterWidth, 0), MouseFilter = MouseFilterEnum.Stop };
            _gutter.AddThemeConstantOverride("separation", 0);
            _gutter.MouseDefaultCursorShape = CursorShape.PointingHand;
            _gutter.GuiInput += OnGutterInput;
            row.AddChild(_gutter);

            var dotSlot = new CenterContainer { CustomMinimumSize = new Vector2(14, 0), MouseFilter = MouseFilterEnum.Ignore };
            var dot = new Panel { CustomMinimumSize = new Vector2(8, 8), MouseFilter = MouseFilterEnum.Ignore };
            dot.AddThemeStyleboxOverride("panel", StudioStyles.Breakpoint(line.HasBreakpoint));
            dotSlot.AddChild(dot);
            _gutter.AddChild(dotSlot);

            var current = line.IsCurrent;
            var number = StudioStyles.Text(line.Number.ToString(), UiTokens.FontBodySmall, current ? UiTokens.AccentBright : UiTokens.Faint, mono: true);
            number.CustomMinimumSize = new Vector2(28, 0);
            number.HorizontalAlignment = HorizontalAlignment.Right;
            _gutter.AddChild(number);

            var mark = StudioStyles.Text(current ? " ▶" : "", UiTokens.FontTiny, UiTokens.AccentBright);
            mark.CustomMinimumSize = new Vector2(16, 0);
            mark.VerticalAlignment = VerticalAlignment.Center;
            _gutter.AddChild(mark);

            var source = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = false,
                CustomMinimumSize = new Vector2(0, LineHeight),
                ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.Off,
                ClipContents = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
                Text = StudioSyntax.ToBbCode(line.Text) + ErrorSuffix(line),
            };
            source.AddThemeFontOverride("normal_font", StudioStyles.Mono);
            source.AddThemeFontSizeOverride("normal_font_size", UiTokens.FontBodySmall);
            source.AddThemeColorOverride("default_color", line.IsDone ? UiTokens.Muted : UiTokens.Body);
            if (line.IsDone)
                source.Modulate = new Color(1f, 1f, 1f, 0.6f);
            row.AddChild(source);
        }

        private static string ErrorSuffix(StudioCodeLine line)
        {
            if (line.Error == null)
                return string.Empty;
            return $"  [color=#{UiTokens.DangerText.ToHtml(false)}]# {line.Error.Resolve().Replace("[", "[lb]")}[/color]";
        }

        private void OnGutterInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
                return;

            _gutter.AcceptEvent();
            BreakpointToggled?.Invoke(_line);
        }
    }
}
