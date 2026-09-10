using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Base for tool popup windows with dirty-state tracking and custom borderless
    /// chrome (title bar with minimize/maximize/close, drag-to-move, edge resize).
    /// Subclasses build their UI in the constructor exactly as before — the chrome
    /// is applied transparently by <see cref="SetupChrome"/> after construction.
    /// </summary>
    public abstract partial class ToolWindow : Window
    {
        // ── existing fields ──────────────────────────────────────────

        /// <summary>
        /// Whether the window has unsaved changes. When <c>true</c>, the title bar shows a "*" prefix
        /// and <see cref="HandleCloseRequested"/> prompts for confirmation before closing.
        /// </summary>
        protected bool _dirty;
        private string _baseTitle;

        // ── chrome fields ───────────────────────────────────────────

        private bool _chromeBuilt;

        private bool _isMaximized;
        private Vector2I _restorePosition;
        private Vector2I _restoreSize;

        private Label _titleLabel;
        private Button _maximizeBtn;
        private HBoxContainer _titleBar;

        // drag
        private bool _draggingTitle;
        private Vector2 _titleDragMouseStart;
        private Vector2I _titleDragWindowStart;

        // resize
        private enum ResizeEdge { None, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }
        private bool _resizing;
        private ResizeEdge _resizeEdge;
        private Vector2 _resizeMouseStart;
        private Vector2I _resizePosStart;
        private Vector2I _resizeSizeStart;

        private const int EDGE_ZONE = 6;
        private const int CORNER_ZONE = 14;
        private const int TITLE_BAR_HEIGHT = 32;

        // ── viewport sizing ─────────────────────────────────────────

        private static Vector2 GetGameViewportSize()
        {
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            var vpSize = tree?.Root?.GetVisibleRect().Size;
            if (vpSize.HasValue && vpSize.Value.X > 0 && vpSize.Value.Y > 0)
                return vpSize.Value;

            return DisplayServer.WindowGetSize();
        }

        // ── public API ──────────────────────────────────────────────

        /// <summary>
        /// Creates a new instance of <typeparamref name="T"/> and displays it as a centered popup
        /// on the scene tree root. Returns the window so the caller can inspect it after closing.
        /// </summary>
        public static T Open<T>() where T : ToolWindow, new()
        {
            var window = new T();
            window.SetupChrome();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered((Vector2I)GetGameViewportSize());
            GameLogger.Log("TOOL_WINDOW", $"Open<{typeof(T).Name}> gameViewport=({GetGameViewportSize().X:F0},{GetGameViewportSize().Y:F0}) physWindow=({DisplayServer.WindowGetSize().X},{DisplayServer.WindowGetSize().Y}) windowSize=({window.Size.X},{window.Size.Y})");
            return window;
        }

        /// <summary>
        /// Displays an already-constructed <see cref="Window"/> as a centered popup on the scene
        /// tree root. Used when the window was created via <see cref="IToolProvider.CreateWindow"/>.
        /// </summary>
        public static Window ShowWindow(Window window)
        {
            if (window is ToolWindow tw)
                tw.SetupChrome();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered((Vector2I)GetGameViewportSize());
            GameLogger.Log("TOOL_WINDOW", $"ShowWindow gameViewport=({GetGameViewportSize().X:F0},{GetGameViewportSize().Y:F0}) physWindow=({DisplayServer.WindowGetSize().X},{DisplayServer.WindowGetSize().Y}) windowSize=({window.Size.X},{window.Size.Y})");
            return window;
        }

        // ── constructor & lifecycle ─────────────────────────────────

        protected ToolWindow(string titleKey)
        {
            _baseTitle = TranslationServer.Translate(titleKey);
            Title = _baseTitle;
            Unresizable = false;
            CloseRequested += HandleCloseRequested;
        }

        public override void _ExitTree()
        {
            Cleanup();
        }

        /// <summary>
        /// Override to release resources when the window is closed. Called from <c>_ExitTree</c>.
        /// </summary>
        protected virtual void Cleanup() { }

        // ── chrome setup ────────────────────────────────────────────

        /// <summary>
        /// Wraps all currently-added children inside a custom chrome layout
        /// (title bar + border + resize zones). Called automatically by
        /// <see cref="Open{T}"/> and <see cref="ShowWindow"/> after the
        /// subclass constructor has built its UI. Safe to call only once.
        /// </summary>
        internal void SetupChrome()
        {
            if (_chromeBuilt)
                return;
            _chromeBuilt = true;

            Borderless = true;
            MinSize = new Vector2I(800, 400);
            MaxSize = (Vector2I)GetGameViewportSize();

            GameLogger.Log("TOOL_WINDOW", $"SetupChrome gameViewport=({GetGameViewportSize().X:F0},{GetGameViewportSize().Y:F0}) physWindow=({DisplayServer.WindowGetSize().X},{DisplayServer.WindowGetSize().Y}) maxSize=({MaxSize.X},{MaxSize.Y})");

            var existingChildren = new List<Node>();
            foreach (Node child in GetChildren())
                existingChildren.Add(child);
            foreach (var child in existingChildren)
                RemoveChild(child);

            var rootPanel = BuildRootPanel();

            var rootVBox = new VBoxContainer();
            rootVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rootVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            rootPanel.AddChild(rootVBox);

            var titleBar = BuildTitleBar();
            rootVBox.AddChild(titleBar);

            var contentArea = new Control
            {
                Name = "ContentArea",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rootVBox.AddChild(contentArea);

            foreach (var child in existingChildren)
                contentArea.AddChild(child);

            AddResizeZones();
        }

        // ── dirty tracking ──────────────────────────────────────────

        /// <summary>
        /// Marks the window as having unsaved changes. Prepends "* " to the title bar text.
        /// </summary>
        protected void SetDirty()
        {
            if (!_dirty)
            {
                _dirty = true;
                var text = "* " + _baseTitle;
                Title = text;
                if (_titleLabel != null)
                    _titleLabel.Text = text;
            }
        }

        /// <summary>
        /// Clears the dirty flag and restores the original title bar text.
        /// </summary>
        protected void MarkClean()
        {
            _dirty = false;
            Title = _baseTitle;
            if (_titleLabel != null)
                _titleLabel.Text = _baseTitle;
        }

        // ── unsaved-changes dialogs ─────────────────────────────────

        protected void ShowUnsavedDialog(Action onDiscard)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("tools/unsaved_title");
            dialog.DialogText = TranslationServer.Translate("tools/unsaved_text");
            dialog.Confirmed += () => { dialog.QueueFree(); onDiscard(); };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
        }

        protected bool ConfirmDiscardUnsaved()
        {
            if (!_dirty)
                return true;

            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("tools/unsaved_title");
            dialog.DialogText = TranslationServer.Translate("tools/unsaved_text");
            AddChild(dialog);

            bool result = false;
            dialog.Confirmed += () => { result = true; dialog.QueueFree(); };
            dialog.Canceled += () => { result = false; dialog.QueueFree(); };
            dialog.PopupCentered();

            return result;
        }

        private void HandleCloseRequested()
        {
            if (_dirty)
                ShowUnsavedDialog(() => QueueFree());
            else
                QueueFree();
        }

        // ── layout helpers (unchanged API) ──────────────────────────

        protected static VBoxContainer CreateFillContainer()
        {
            var c = new VBoxContainer();
            c.AnchorRight = 1f;
            c.AnchorBottom = 1f;
            c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            return c;
        }

        protected static HBoxContainer CreateToolbar()
        {
            var t = new HBoxContainer();
            t.AddThemeConstantOverride("separation", 8);
            return t;
        }

        protected static PanelContainer CreateSidebar(VBoxContainer content, int width = 280)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(width, 100);
            panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var scroll = new ScrollContainer();
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            panel.AddChild(scroll);
            scroll.AddChild(content);

            return panel;
        }

        protected static Font GetMonospaceFont()
        {
            try
            {
                if (ResourceLoader.Exists("res://assets/fonts/RobotoMono.ttf"))
                    return ResourceLoader.Load<FontFile>("res://assets/fonts/RobotoMono.ttf");
            }
            catch { }
            return ThemeDB.FallbackFont;
        }

        // ── chrome building ─────────────────────────────────────────

        private PanelContainer BuildRootPanel()
        {
            var panel = new PanelContainer
            {
                Name = "RootPanel",
                AnchorRight = 1f,
                AnchorBottom = 1f
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.11f, 0.11f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderColor = new Color(0.22f, 0.22f, 0.22f),
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginBottom = 4,
                ContentMarginTop = 4
            };
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);

            return panel;
        }

        private PanelContainer BuildTitleBar()
        {
            _titleBar = new HBoxContainer
            {
                Name = "TitleBar",
                CustomMinimumSize = new Vector2(0, TITLE_BAR_HEIGHT)
            };

            _titleLabel = new Label
            {
                Text = _dirty ? "* " + _baseTitle : _baseTitle,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.82f));
            _titleBar.AddChild(_titleLabel);

            _maximizeBtn = CreateTitleButton("\u25A1", OnMaximizeToggle,
                new Color(0.235f, 0.235f, 0.235f));
            _titleBar.AddChild(_maximizeBtn);

            var closeBtn = CreateTitleButton("\u2715", OnClose,
                new Color(0.91f, 0.07f, 0.14f));
            _titleBar.AddChild(closeBtn);

            _titleBar.GuiInput += OnTitleBarInput;

            var titlePanel = new PanelContainer();
            titlePanel.AddChild(_titleBar);

            var titleBg = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.18f, 0.18f, 1f),
                ContentMarginLeft = 10,
                ContentMarginRight = 2,
            };
            titlePanel.AddThemeStyleboxOverride("panel", titleBg);

            return titlePanel;
        }

        private static Button CreateTitleButton(string text, Action pressed, Color hoverColor)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(36, 28),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                Flat = true
            };

            btn.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.82f));
            btn.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
            btn.AddThemeColorOverride("font_hover_pressed_color", new Color(1f, 1f, 1f));
            btn.AddThemeFontSizeOverride("font_size", 14);

            var normalStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
            btn.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = new StyleBoxFlat { BgColor = hoverColor };
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
            btn.AddThemeStyleboxOverride("pressed", hoverStyle);

            btn.AddThemeStyleboxOverride("focus", normalStyle);

            btn.Pressed += pressed;
            return btn;
        }

        // ── button actions ──────────────────────────────────────────

        private void OnMaximizeToggle()
        {
            if (_isMaximized)
                RestoreWindow();
            else
                MaximizeWindow();
        }

        private void MaximizeWindow()
        {
            _restorePosition = Position;
            _restoreSize = Size;
            _isMaximized = true;
            _maximizeBtn.Text = "\u2750";

            Position = Vector2I.Zero;
            Size = (Vector2I)GetGameViewportSize();
            GameLogger.Log("TOOL_WINDOW", $"MaximizeWindow gameViewport=({GetGameViewportSize().X:F0},{GetGameViewportSize().Y:F0}) physWindow=({DisplayServer.WindowGetSize().X},{DisplayServer.WindowGetSize().Y}) newSize=({Size.X},{Size.Y})");
        }

        private void RestoreWindow()
        {
            _isMaximized = false;
            _maximizeBtn.Text = "\u25A1";

            Position = _restorePosition;
            Size = _restoreSize;
        }

        private void OnClose()
        {
            HandleCloseRequested();
        }

        // ── title bar drag ──────────────────────────────────────────

        private void OnTitleBarInput(InputEvent evt)
        {
            if (evt is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        if (mb.DoubleClick)
                        {
                            OnMaximizeToggle();
                            return;
                        }

                        if (_isMaximized)
                        {
                            _isMaximized = false;
                            _maximizeBtn.Text = "\u25A1";
                            var pos = DisplayServer.MouseGetPosition();
                            var viewportSize = GetGameViewportSize();
                            var ratio = pos.X / viewportSize.X;
                            _titleDragWindowStart = new Vector2I(
                                Mathf.RoundToInt(pos.X - _restoreSize.X * ratio),
                                Mathf.RoundToInt(pos.Y - 4));
                            _titleDragMouseStart = new Vector2(pos.X, pos.Y);
                        }
                        else
                        {
                            _titleDragWindowStart = Position;
                            var mp = DisplayServer.MouseGetPosition();
                            _titleDragMouseStart = new Vector2(mp.X, mp.Y);
                        }
                        _draggingTitle = true;
                    }
                    else
                    {
                        _draggingTitle = false;
                    }
                }
            }
            else if (evt is InputEventMouseMotion mm && _draggingTitle)
            {
                var mp = DisplayServer.MouseGetPosition();
                var delta = new Vector2(mp.X, mp.Y) - _titleDragMouseStart;
                Position = new Vector2I(
                    _titleDragWindowStart.X + Mathf.RoundToInt(delta.X),
                    _titleDragWindowStart.Y + Mathf.RoundToInt(delta.Y));
            }
        }

        // ── resize zones ────────────────────────────────────────────

        private void AddResizeZones()
        {
            AddEdgeH("ResizeLeft",  ResizeEdge.Left,     0f,  0f,  0f, EDGE_ZONE);
            AddEdgeH("ResizeRight", ResizeEdge.Right,    1f,  1f, -EDGE_ZONE, 0f);
            AddEdgeV("ResizeTop",   ResizeEdge.Top,      0f,  0f,  0f, EDGE_ZONE);
            AddEdgeV("ResizeBottom",ResizeEdge.Bottom,   1f,  1f, -EDGE_ZONE, 0f);

            AddCornerZone("ResizeTL", ResizeEdge.TopLeft,
                0f, 0f, CORNER_ZONE, CORNER_ZONE, Control.CursorShape.Fdiagsize);
            AddCornerZone("ResizeTR", ResizeEdge.TopRight,
                1f, 0f, -CORNER_ZONE, CORNER_ZONE, Control.CursorShape.Bdiagsize);
            AddCornerZone("ResizeBL", ResizeEdge.BottomLeft,
                0f, 1f, CORNER_ZONE, -CORNER_ZONE, Control.CursorShape.Bdiagsize);
            AddCornerZone("ResizeBR", ResizeEdge.BottomRight,
                1f, 1f, -CORNER_ZONE, -CORNER_ZONE, Control.CursorShape.Fdiagsize);
        }

        private void AddEdgeH(string name, ResizeEdge edge,
            float anchorLeft, float anchorRight, float offsetLeft, float offsetRight)
        {
            var zone = MakeResizeZone(name, edge, Control.CursorShape.Hsize);
            zone.AnchorLeft = anchorLeft;
            zone.AnchorRight = anchorRight;
            zone.OffsetLeft = offsetLeft;
            zone.OffsetRight = offsetRight;
            zone.AnchorTop = 0f;
            zone.AnchorBottom = 1f;
            zone.OffsetTop = CORNER_ZONE;
            zone.OffsetBottom = -CORNER_ZONE;
            AddChild(zone);
        }

        private void AddEdgeV(string name, ResizeEdge edge,
            float anchorTop, float anchorBottom, float offsetTop, float offsetBottom)
        {
            var zone = MakeResizeZone(name, edge, Control.CursorShape.Vsize);
            zone.AnchorLeft = 0f;
            zone.AnchorRight = 1f;
            zone.OffsetLeft = CORNER_ZONE;
            zone.OffsetRight = -CORNER_ZONE;
            zone.AnchorTop = anchorTop;
            zone.AnchorBottom = anchorBottom;
            zone.OffsetTop = offsetTop;
            zone.OffsetBottom = offsetBottom;
            AddChild(zone);
        }

        private void AddCornerZone(string name, ResizeEdge edge,
            float anchorH, float anchorV, int offsetW, int offsetH,
            Control.CursorShape cursor)
        {
            var zone = MakeResizeZone(name, edge, cursor);

            zone.AnchorLeft = anchorH;
            zone.AnchorRight = anchorH;
            zone.AnchorTop = anchorV;
            zone.AnchorBottom = anchorV;

            if (offsetW >= 0)
            {
                zone.OffsetLeft = 0;
                zone.OffsetRight = offsetW;
            }
            else
            {
                zone.OffsetLeft = offsetW;
                zone.OffsetRight = 0;
            }

            if (offsetH >= 0)
            {
                zone.OffsetTop = 0;
                zone.OffsetBottom = offsetH;
            }
            else
            {
                zone.OffsetTop = offsetH;
                zone.OffsetBottom = 0;
            }

            AddChild(zone);
        }

        private Control MakeResizeZone(string name, ResizeEdge edge, Control.CursorShape cursor)
        {
            var zone = new Control
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = cursor
            };

            zone.GuiInput += (InputEvent evt) => HandleResizeInput(evt, edge);
            return zone;
        }

        private void HandleResizeInput(InputEvent evt, ResizeEdge edge)
        {
            if (evt is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _resizing = true;
                    _resizeEdge = edge;
                    var mp = DisplayServer.MouseGetPosition();
                    _resizeMouseStart = new Vector2(mp.X, mp.Y);
                    _resizePosStart = Position;
                    _resizeSizeStart = Size;
                }
                else
                {
                    _resizing = false;
                    _resizeEdge = ResizeEdge.None;
                }
            }
            else if (evt is InputEventMouseMotion && _resizing)
            {
                var mp = DisplayServer.MouseGetPosition();
                ApplyResize(new Vector2(mp.X, mp.Y));
            }
        }

        private void ApplyResize(Vector2 globalMouse)
        {
            var delta = globalMouse - _resizeMouseStart;

            int newX = _resizePosStart.X;
            int newY = _resizePosStart.Y;
            int newW = _resizeSizeStart.X;
            int newH = _resizeSizeStart.Y;

            switch (_resizeEdge)
            {
                case ResizeEdge.Left:
                    newX = _resizePosStart.X + Mathf.RoundToInt(delta.X);
                    newW = _resizeSizeStart.X - Mathf.RoundToInt(delta.X);
                    break;
                case ResizeEdge.Right:
                    newW = _resizeSizeStart.X + Mathf.RoundToInt(delta.X);
                    break;
                case ResizeEdge.Top:
                    newY = _resizePosStart.Y + Mathf.RoundToInt(delta.Y);
                    newH = _resizeSizeStart.Y - Mathf.RoundToInt(delta.Y);
                    break;
                case ResizeEdge.Bottom:
                    newH = _resizeSizeStart.Y + Mathf.RoundToInt(delta.Y);
                    break;
                case ResizeEdge.TopLeft:
                    newX = _resizePosStart.X + Mathf.RoundToInt(delta.X);
                    newY = _resizePosStart.Y + Mathf.RoundToInt(delta.Y);
                    newW = _resizeSizeStart.X - Mathf.RoundToInt(delta.X);
                    newH = _resizeSizeStart.Y - Mathf.RoundToInt(delta.Y);
                    break;
                case ResizeEdge.TopRight:
                    newY = _resizePosStart.Y + Mathf.RoundToInt(delta.Y);
                    newW = _resizeSizeStart.X + Mathf.RoundToInt(delta.X);
                    newH = _resizeSizeStart.Y - Mathf.RoundToInt(delta.Y);
                    break;
                case ResizeEdge.BottomLeft:
                    newX = _resizePosStart.X + Mathf.RoundToInt(delta.X);
                    newW = _resizeSizeStart.X - Mathf.RoundToInt(delta.X);
                    newH = _resizeSizeStart.Y + Mathf.RoundToInt(delta.Y);
                    break;
                case ResizeEdge.BottomRight:
                    newW = _resizeSizeStart.X + Mathf.RoundToInt(delta.X);
                    newH = _resizeSizeStart.Y + Mathf.RoundToInt(delta.Y);
                    break;
            }

            newX = Mathf.Clamp(newX, 0, MaxSize.X - 100);
            newY = Mathf.Clamp(newY, 0, MaxSize.Y - 100);
            newW = Mathf.Clamp(newW, MinSize.X, MaxSize.X);
            newH = Mathf.Clamp(newH, MinSize.Y, MaxSize.Y);

            if (newX + newW > MaxSize.X)
                newX = MaxSize.X - newW;
            if (newY + newH > MaxSize.Y)
                newY = MaxSize.Y - newH;

            Position = new Vector2I(newX, newY);
            Size = new Vector2I(newW, newH);
        }
    }
}
