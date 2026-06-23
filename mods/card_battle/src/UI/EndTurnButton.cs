using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    public partial class EndTurnButton : ModWidget
    {
        private Button _button;

        public event System.Action OnPressed;

        protected override void Construct()
        {
            CustomMinimumSize = new Vector2(160, 50);
            Size = new Vector2(160, 50);

            _button = new Button();
            _button.Text = TranslationServer.Translate("battle/end_turn");
            _button.SetAnchorsPreset(LayoutPreset.FullRect);
            _button.AddThemeFontSizeOverride("font_size", 16);
            _button.Pressed += () => OnPressed?.Invoke();
            AddChild(_button);
        }

        public void SetVisible(bool visible)
        {
            if (_button != null)
                _button.Visible = visible;
        }
    }
}
