using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Defines the six states of the battle UI state machine, driving which input
    /// handler is active and what the player sees.
    /// </summary>
    public enum FFBattleState
    {
        /// <summary>No menu open; player can select a character to act.</summary>
        Idle,
        /// <summary>Main action menu (attack/items/defend/escape) is visible.</summary>
        MenuOpen,
        /// <summary>Sub-menu (action list or item list) is visible.</summary>
        SubMenuOpen,
        /// <summary>Player must click a valid target character widget.</summary>
        TargetSelect,
        /// <summary>An animation is playing; all input is blocked.</summary>
        AnimationPlaying,
        /// <summary>Battle has concluded.</summary>
        Ended
    }

    /// <summary>
    /// Input dispatch layer for the FF battle UI. Extends <see cref="InputHandlerNode"/>
    /// so that keyboard events from Godot's input system are forwarded to this node
    /// even in mod assemblies. Routes arrow keys, Enter, Space, and Escape to the
    /// appropriate handler based on current <see cref="State"/>. Also provides
    /// mouse-driven character selection via <see cref="SelectCharacter"/>.
    /// Events are raised to <see cref="FFBattleCore"/> which wires up the actual logic.
    /// </summary>
    public partial class FFBattleController : InputHandlerNode
    {
        private FFBattleState _state = FFBattleState.Idle;

        /// <summary>Reference to the player-side character panel for idle-state character selection.</summary>
        public FFPlayerPanel PlayerPanel { get; set; }
        /// <summary>Reference to the enemy-side panel for visual context (not directly used for input).</summary>
        public FFEnemyPanel EnemyPanel { get; set; }
        /// <summary>The main menu panel; <see cref="HandleMenuInput"/> delegates up/down/confirm to it.</summary>
        public FFMenuPanel MenuPanel { get; set; }
        /// <summary>The sub-menu panel; <see cref="HandleSubMenuInput"/> delegates to it.</summary>
        public FFMenuPanel SubMenuPanel { get; set; }

        /// <summary>Raised when a character widget is selected (via mouse click or keyboard).</summary>
        public event System.Action<FFCharacterWidget> OnCharacterSelected;
        /// <summary>Raised when a menu item is confirmed. The int is the entry index.</summary>
        public event System.Action<int> OnMenuAction;
        /// <summary>Raised when a sub-menu item is confirmed.</summary>
        public event System.Action<int> OnSubMenuAction;
        /// <summary>Raised when Escape is pressed while the main menu is open.</summary>
        public event System.Action OnMenuCancel;
        /// <summary>Raised when Escape is pressed while a sub-menu is open.</summary>
        public event System.Action OnSubMenuCancel;
        /// <summary>Raised when a target widget is confirmed in target-select mode.</summary>
        public event System.Action<FFCharacterWidget> OnTargetSelected;

        /// <summary>
        /// Current UI state. Setting this property logs the transition via
        /// <see cref="GameLogger"/> for debugging battle flow.
        /// </summary>
        public FFBattleState State
        {
            get => _state;
            set
            {
                _state = value;
                Cthangover.Core.Utils.GameLogger.Log("FF_BATTLE", $"Controller state → {value}", Cthangover.Core.Utils.LogLevel.Debug);
            }
        }

        /// <summary>Ensures the controller processes input even when the scene tree is paused.</summary>
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

        /// <summary>
        /// Selects a character widget, raising <see cref="OnCharacterSelected"/>.
        /// Only valid in <see cref="FFBattleState.Idle"/> state.
        /// </summary>
        public void SelectCharacter(FFCharacterWidget widget)
        {
            if (State != FFBattleState.Idle)
                return;

            OnCharacterSelected?.Invoke(widget);
        }
    }
}
