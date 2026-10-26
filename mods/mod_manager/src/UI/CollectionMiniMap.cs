namespace Mods.ModManager
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// Scene-positioned overview of the occupied collection canvas. Reads the
    /// existing logical frames; clicking centers the scroll view without changing
    /// composition or persisted layout. Drawing uses the signal-based mod path.
    /// </summary>
    public partial class CollectionMiniMap : ModWidget
    {
        private CollectionSchemeCanvas _canvas;
        private StyleBoxFlat _style;

        /// <summary>Attaches the live canvas; this control owns no copy of its layout.</summary>
        public void Bind(CollectionSchemeCanvas canvas)
        {
            EnsureConstructed();
            _canvas = canvas;
            QueueRedraw();
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            ClipContents = true;
            _style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Surface2, UiTokens.LineStrong);
            GuiInput += OnMapInput;
            Connect("draw", Callable.From((Action)DrawMap));
        }

        private void DrawMap()
        {
            DrawStyleBox(_style, new Rect2(Vector2.Zero, Size));
            if (_canvas == null || !GodotObject.IsInstanceValid(_canvas))
                return;

            var scale = (Size - Vector2.One * 8f) / _canvas.ContentBounds;
            foreach (var pair in _canvas.GroupFrames)
            {
                var frame = pair.Value;
                if (!frame.X.HasValue || !frame.Y.HasValue)
                    continue;
                var rect = new Rect2(new Vector2(frame.X.Value, frame.Y.Value) * scale + Vector2.One * 4f,
                    new Vector2(frame.W, frame.H) * scale);
                DrawRect(rect, UiTokens.Surface3);
                DrawRect(rect, pair.Key == _canvas.SelectedGroupKey ? UiTokens.Accent : UiTokens.LineStrong, false);
            }
            var visible = _canvas.VisibleBounds;
            DrawRect(new Rect2(visible.Position * scale + Vector2.One * 4f, visible.Size * scale), UiTokens.Accent, false);
        }

        private void OnMapInput(InputEvent @event)
        {
            if (_canvas == null || @event is not InputEventMouseButton button
                || !button.Pressed || button.ButtonIndex != MouseButton.Left)
                return;
            var scale = (Size - Vector2.One * 8f) / _canvas.ContentBounds;
            _canvas.CenterOn((button.Position - Vector2.One * 4f) / scale);
            AcceptEvent();
        }
    }
}
