namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Godot;

    /// <summary>
    /// One script line of the author mode (<c>.ct-code__line</c>): gutter with
    /// the breakpoint dot, line number and the ▶ marker of the current line,
    /// then the coloured source. A click on the gutter toggles the breakpoint,
    /// like in an IDE. The ▶ marker of the current line is a handle of its own:
    /// dragging it onto another line is the IDE's "set next statement" — the
    /// view only reports where the mouse is (<see cref="MarkerDragged"/>,
    /// <see cref="MarkerDropped"/> in canvas coordinates); the page decides
    /// which line is under it. Godot keeps sending motion and release to the
    /// control the press started on, so the marker sees the whole drag even
    /// over other lines. A parse error is shown as a faint comment after the
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
        private readonly Label _marker;
        private bool _dragging;

        /// <summary>The line this row shows.</summary>
        public StudioCodeLine Line => _line;

        /// <summary>The clickable gutter (breakpoint dot, number, marker).</summary>
        public Control Gutter => _gutter;

        /// <summary>The ▶ marker of the current line, the handle dragged to choose the next line.</summary>
        public Control Marker => _marker;

        /// <summary>Raised when the gutter is clicked.</summary>
        public event Action<StudioCodeLine> BreakpointToggled;

        /// <summary>Raised while the marker is dragged, with the mouse position in canvas coordinates.</summary>
        public event Action<Vector2> MarkerDragged;

        /// <summary>Raised when the dragged marker is released, with the mouse position in canvas coordinates.</summary>
        public event Action<Vector2> MarkerDropped;

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

            _marker = StudioStyles.Text(current ? " ▶" : "", UiTokens.FontTiny, UiTokens.AccentBright);
            _marker.CustomMinimumSize = new Vector2(16, 0);
            _marker.VerticalAlignment = VerticalAlignment.Center;
            if (current)
            {
                _marker.MouseFilter = MouseFilterEnum.Stop;
                _marker.MouseDefaultCursorShape = CursorShape.Drag;
                _marker.TooltipText = TranslationServer.Translate("studio/drag_next_line");
                _marker.GuiInput += OnMarkerInput;
            }
            _gutter.AddChild(_marker);

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

        /// <summary>Marks the row as the place the dragged marker would land (or clears the mark).</summary>
        public void ShowDropTarget(bool shown)
        {
            AddThemeStyleboxOverride("panel", shown ? StudioStyles.CodeLineDropTarget() : StudioStyles.CodeLine(_line));
        }

        private void OnMarkerInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse when mouse.Pressed:
                    _dragging = true;
                    _marker.AcceptEvent();
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse when _dragging:
                    _dragging = false;
                    _marker.AcceptEvent();
                    MarkerDropped?.Invoke(_marker.GetGlobalTransform() * mouse.Position);
                    break;
                case InputEventMouseMotion motion when _dragging:
                    _marker.AcceptEvent();
                    MarkerDragged?.Invoke(_marker.GetGlobalTransform() * motion.Position);
                    break;
            }
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
