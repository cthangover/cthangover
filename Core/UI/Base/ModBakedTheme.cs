using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Base
{
    public partial class ModBakedTheme : Node
    {
        public override void _Ready()
        {
            var parent = GetParent() as Control;
            if (parent == null)
                return;

            var gui = UITextureFactory.Instance.Get("gui");
            var black = UITextureFactory.Instance.Get("base/black");
            if (gui == null || black == null)
                return;

            var theme = new Theme();

            var btnNormal = MakeStyle(gui, new Color(0.15f, 0.15f, 0.2f, 0.9f), new Rect2(1, 19, 19, 33), 8);
            theme.SetStylebox("normal", "Button", btnNormal);

            var btnHover = MakeStyle(gui, new Color(0.25f, 0.25f, 0.35f, 1.0f), new Rect2(1, 19, 19, 33), 8);
            theme.SetStylebox("hover", "Button", btnHover);

            var btnPressed = MakeStyle(gui, new Color(0.35f, 0.35f, 0.45f, 1.0f), new Rect2(1, 19, 19, 33), 8);
            theme.SetStylebox("pressed", "Button", btnPressed);

            var btnDisabled = MakeStyle(gui, new Color(0.08f, 0.08f, 0.12f, 0.6f), new Rect2(1, 19, 19, 33), 8);
            theme.SetStylebox("disabled", "Button", btnDisabled);

            var panel = MakeStyle(black, new Color(0.08f, 0.08f, 0.12f, 0.85f), null, 2);
            theme.SetStylebox("panel", "Panel", panel);

            var lineEditNormal = MakeStyle(gui, new Color(0.25f, 0.3f, 0.5f, 1.0f), null, 4);
            theme.SetStylebox("normal", "LineEdit", lineEditNormal);

            theme.SetStylebox("focus", "LineEdit", btnHover);
            theme.SetStylebox("grabber", "HSlider", lineEditNormal);
            theme.SetStylebox("grabber_highlight", "HSlider", btnHover);
            theme.SetStylebox("grabber", "VSlider", lineEditNormal);
            theme.SetStylebox("grabber_highlight", "VSlider", btnHover);
            theme.SetStylebox("panel", "Popup", btnDisabled);
            theme.SetStylebox("panel", "Tooltip", btnDisabled);
            theme.SetStylebox("panel", "Window", new StyleBoxEmpty());

            theme.SetColor("font_color", "", new Color(0.85f, 0.85f, 0.9f, 1.0f));
            theme.SetColor("font_hover_color", "", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            theme.SetColor("font_pressed_color", "", new Color(0.7f, 0.7f, 0.75f, 1.0f));

            parent.Theme = theme;
        }

        private static StyleBoxTexture MakeStyle(Texture2D texture, Color color, Rect2? region, int patch)
        {
            var style = new StyleBoxTexture
            {
                Texture = texture,
                ModulateColor = color,
            };
            if (region.HasValue)
                style.RegionRect = region.Value;
            style.SetContentMargin(Side.Left, patch);
            style.SetContentMargin(Side.Right, patch);
            style.SetContentMargin(Side.Top, patch);
            style.SetContentMargin(Side.Bottom, patch);
            return style;
        }
    }
}
