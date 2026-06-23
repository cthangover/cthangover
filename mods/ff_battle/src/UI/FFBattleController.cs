using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    public enum FFBattleState
    {
        Idle,
        MenuOpen,
        SubMenuOpen,
        TargetSelect,
        AnimationPlaying,
        Ended
    }

    public partial class FFBattleController : InputHandlerNode
    {
        private FFBattleState _state = FFBattleState.Idle;

        public FFPlayerPanel PlayerPanel { get; set; }
        public FFEnemyPanel EnemyPanel { get; set; }
        public FFMenuPanel MenuPanel { get; set; }
        public FFMenuPanel SubMenuPanel { get; set; }

        public event System.Action<FFCharacterWidget> OnCharacterSelected;
        public event System.Action<int> OnMenuAction;
        public event System.Action<int> OnSubMenuAction;
        public event System.Action OnMenuCancel;
        public event System.Action OnSubMenuCancel;
        public event System.Action<FFCharacterWidget> OnTargetSelected;

        public FFBattleState State
        {
            get => _state;
            set
            {
                _state = value;
                Cthangover.Core.Utils.GameLogger.Log("FF_BATTLE", $"Controller state → {value}", Cthangover.Core.Utils.LogLevel.Debug);
            }
        }

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
        }

        protected override void OnInput(InputEvent @event)
        {
            if (State == FFBattleState.AnimationPlaying || State == FFBattleState.Ended)
                return;

            if (@event is InputEventKey key && key.Pressed)
            {
                HandleKeyboardInput(key);
                if (State != FFBattleState.Idle)
                    GetViewport().SetInputAsHandled();
            }
        }

        private void HandleKeyboardInput(InputEventKey key)
        {
            switch (State)
            {
                case FFBattleState.Idle:
                    HandleIdleInput(key);
                    break;

                case FFBattleState.MenuOpen:
                    HandleMenuInput(key);
                    break;

                case FFBattleState.SubMenuOpen:
                    HandleSubMenuInput(key);
                    break;

                case FFBattleState.TargetSelect:
                    HandleTargetInput(key);
                    break;
            }
        }

        private void HandleIdleInput(InputEventKey key)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.Space)
            {
                SelectFirstAvailableCharacter();
            }
        }

        private void HandleMenuInput(InputEventKey key)
        {
            switch (key.Keycode)
            {
                case Key.Up:
                case Key.W:
                    MenuPanel?.SelectPrevious();
                    break;

                case Key.Down:
                case Key.S:
                    MenuPanel?.SelectNext();
                    break;

                case Key.Enter:
                case Key.Space:
                    MenuPanel?.ConfirmSelection();
                    break;

                case Key.Escape:
                    OnMenuCancel?.Invoke();
                    break;
            }
        }

        private void HandleSubMenuInput(InputEventKey key)
        {
            switch (key.Keycode)
            {
                case Key.Up:
                case Key.W:
                    SubMenuPanel?.SelectPrevious();
                    break;

                case Key.Down:
                case Key.S:
                    SubMenuPanel?.SelectNext();
                    break;

                case Key.Enter:
                case Key.Space:
                    SubMenuPanel?.ConfirmSelection();
                    break;

                case Key.Escape:
                    OnSubMenuCancel?.Invoke();
                    break;
            }
        }

        private void HandleTargetInput(InputEventKey key)
        {
            if (key.Keycode == Key.Escape)
            {
                OnMenuCancel?.Invoke();
            }
        }

        private void SelectFirstAvailableCharacter()
        {
            if (PlayerPanel == null)
                return;

            foreach (var widget in PlayerPanel.Widgets)
            {
                if (!widget.IsDead && widget.Card?.Attributes?.Point?.Value > 0)
                {
                    OnCharacterSelected?.Invoke(widget);
                    return;
                }
            }
        }

        public void SelectCharacter(FFCharacterWidget widget)
        {
            if (State != FFBattleState.Idle)
                return;

            OnCharacterSelected?.Invoke(widget);
        }
    }
}
