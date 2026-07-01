using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle
{
    /// <summary>
    /// Core orchestration class for the Final Fantasy-style turn-based battle system.
    /// Manages the full battle lifecycle: player turn with menu-driven action selection,
    /// enemy AI turn execution, target selection mode, escape mechanics, and win/loss
    /// condition checks. Coordinates between <see cref="FFPlayerPanel"/>,
    /// <see cref="FFEnemyPanel"/>, <see cref="FFMenuPanel"/>, and
    /// <see cref="FFBattleController"/> for UI interaction. Registered via
    /// <see cref="IBattleCore.Id"/> as <c>"ff_battle"</c>.
    /// </summary>
    public class FFBattleCore : IBattleCore
    {
        /// <summary>Unique identifier for this battle system implementation, used by the core engine to locate it.</summary>
        public string Id => "ff_battle";
        /// <summary>Provides the set of action executors (damage, defence, stun, item) registered for this battle system.</summary>
        public IActionExecutorProvider ActionProvider { get; } = new Actions.FFActionProvider();

        private Character[] _playerChars;
        private Character[] _enemyChars;
        private IBattleContext _ctx;

        private FFPlayerPanel _playerPanel;
        private FFEnemyPanel _enemyPanel;
        private FFMenuPanel _menuPanel;
        private FFBattleController _controller;
        private Control _toolPanel;

        private Button _endTurnButton;

        private FFCharacterWidget _currentCharacter;
        private FFMenuEntry _currentMenuAction;
        private bool _isPlayerTurn;
        private bool _targetSelectMode;
        private bool _targetAlly;
        private System.Action<FFCharacterWidget> _onTargetSelected;

        private const float PLAYER_SCALE = 0.85f;

        /// <summary>
        /// Initialises the battle UI panels, controller, and event wiring. Copies enemy
        /// <see cref="Character"/> instances via <c>Copy()</c> so mutations during battle
        /// do not affect the original data. Registers the <see cref="ActionProvider"/>
        /// with <see cref="ActionExecutorHub"/> and constructs all UI widgets inside
        /// <paramref name="ctx"/>'s root node.
        /// </summary>
        /// <param name="playerChars">The player party characters, consumed directly (not copied).</param>
        /// <param name="enemyChars">Enemy characters — each is shallow-copied for battle isolation.</param>
        /// <param name="ctx">Battle context providing root node and end-battle callback.</param>
        public void Init(Character[] playerChars, Character[] enemyChars, IBattleContext ctx)
        {
            _playerChars = playerChars;
            _ctx = ctx;

            _enemyChars = new Character[enemyChars.Length];
            for (int i = 0; i < enemyChars.Length; i++)
                _enemyChars[i] = enemyChars[i]?.Copy() ?? enemyChars[i];

            ActionExecutorHub.Instance.SetActiveProvider(ActionProvider);

            var root = ctx.RootNode as Node;
            var panel = root.GetNodeOrNull<Control>("Panel") ?? root;
            _toolPanel = root.GetNodeOrNull<Control>("ToolPanel");

            _playerPanel = new FFPlayerPanel { Name = "FFPlayerPanel" };
            _playerPanel.EnsureConstructed();
            panel.AddChild(_playerPanel);

            _enemyPanel = new FFEnemyPanel { Name = "FFEnemyPanel" };
            _enemyPanel.EnsureConstructed();
            panel.AddChild(_enemyPanel);

            _menuPanel = new FFMenuPanel { Name = "FFMenuPanel" };
            _menuPanel.EnsureConstructed();
            _menuPanel.HideMenu();
            panel.AddChild(_menuPanel);

            _endTurnButton = new Button();
            _endTurnButton.Text = TranslationServer.Translate("ff_battle/end_turn");
            _endTurnButton.Pressed += OnEndTurnPressed;
            _endTurnButton.Visible = false;
            _endTurnButton.FocusMode = Control.FocusModeEnum.None;
            _toolPanel?.AddChild(_endTurnButton);

            _controller = new FFBattleController { Name = "FFBattleController" };
            panel.AddChild(_controller);
            _controller.PlayerPanel = _playerPanel;
            _controller.EnemyPanel = _enemyPanel;
            _controller.MenuPanel = _menuPanel;
            _controller.SubMenuPanel = _menuPanel;
            _controller.OnCharacterSelected += OnCharacterSelected;
            _controller.OnMenuCancel += OnMenuCancel;
            _controller.OnTargetSelected += OnTargetSelected;

            _playerPanel.OnWidgetClicked += OnWidgetClicked;
            _enemyPanel.OnEnemyClicked += OnEnemyClicked;
            _enemyPanel.OnEnemyDead += OnEnemyDied;
            _menuPanel.OnItemSelected += OnMenuItemSelected;
            _menuPanel.OnCancelled += OnMenuCancel;
        }

        /// <summary>
        /// Begins the battle sequence. Calculates layout positions and scales for
        /// player/enemy panels based on viewport size. Subscribes to
        /// <see cref="BattleSceneContext.OnBattleCleared"/> for cleanup.
        /// Immediately transitions into the first player turn.
        /// </summary>
        public void Start()
        {
            var root = _ctx.RootNode as Node;
            var viewportSize = root.GetViewport().GetVisibleRect().Size;

            var enemyScale = FFEnemyPanel.CalculateScale(_enemyChars.Length,
                new Vector2(viewportSize.X * 0.9f, viewportSize.Y * 0.5f));

            _enemyPanel.Position = new Vector2(viewportSize.X * 0.05f, 0);
            _enemyPanel.Size = new Vector2(viewportSize.X * 0.9f, viewportSize.Y * 0.5f);

            _playerPanel.Position = new Vector2(0, viewportSize.Y * 0.55f);
            _playerPanel.Size = new Vector2(viewportSize.X * 0.65f, viewportSize.Y * 0.45f);

            _menuPanel.Position = new Vector2(viewportSize.X * 0.66f, viewportSize.Y * 0.58f);
            _menuPanel.Size = new Vector2(viewportSize.X * 0.3f, 160f);

            if (_endTurnButton != null)
                _endTurnButton.Position = new Vector2(viewportSize.X - 180f, viewportSize.Y * 0.92f);

            _playerPanel.Init(_playerChars, PLAYER_SCALE);
            _enemyPanel.Init(_enemyChars, enemyScale);

            BattleSceneContext.Instance.OnBattleCleared += OnBattleCleared;

            _isPlayerTurn = true;
            StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            GameLogger.Log("FF_BATTLE", "Player turn started", LogLevel.Debug);

            _isPlayerTurn = true;
            BattleSceneContext.Instance.IsWait = false;

            _currentCharacter = null;
            _targetSelectMode = false;
            _menuPanel.HideMenu();

            foreach (var widget in _playerPanel.Widgets)
            {
                if (!widget.IsDead)
                {
                    widget.Card.StatusEffectQueue.OnTurnStart();
                    widget.Card.Attributes.Point.Value = widget.Card.Attributes.Point.BaseValue;
                }
                widget.UpdateInfo();
            }

            _endTurnButton.Visible = true;
            _controller.State = FFBattleState.Idle;

            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
        }

        private void OnWidgetClicked(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn)
                return;

            if (_targetSelectMode)
            {
                if (widget.IsDead)
                    return;

                if (_targetAlly && !_playerPanel.Widgets.Contains(widget))
                    return;

                if (!_targetAlly && _playerPanel.Widgets.Contains(widget))
                    return;

                _targetSelectMode = false;
                foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
                foreach (var w in _playerPanel.Widgets) w.ClearHighlight();

                ExecuteMenuAction(widget);
                return;
            }

            _controller.SelectCharacter(widget);
        }

        private void OnCharacterSelected(FFCharacterWidget widget)
        {
            if (widget.IsDead || widget.Card?.Attributes?.Point?.Value <= 0)
                return;

            _currentCharacter = widget;
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            var entries = new List<FFMenuEntry>
            {
                new FFMenuEntry
                {
                    Key = "attack",
                    Label = TranslationServer.Translate("ff_battle/menu_attack"),
                    Enabled = HasAvailableActions()
                },
                new FFMenuEntry
                {
                    Key = "items",
                    Label = TranslationServer.Translate("ff_battle/menu_items"),
                    Enabled = HasUsableItems()
                },
                new FFMenuEntry
                {
                    Key = "defend",
                    Label = TranslationServer.Translate("ff_battle/menu_defend"),
                    Enabled = HasDefendAction()
                },
                new FFMenuEntry
                {
                    Key = "escape",
                    Label = TranslationServer.Translate("ff_battle/menu_escape"),
                    Enabled = true
                }
            };

            _currentMenuAction = null;
            _menuPanel.ShowMenu(entries);
            _controller.State = FFBattleState.MenuOpen;

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            _currentCharacter?.Highlight(new Color(0, 0.6f, 1f, 0.6f));
        }

        private void OnMenuItemSelected(int index)
        {
            if (_menuPanel.Entries == null || index >= _menuPanel.Entries.Count)
                return;

            var entry = _menuPanel.Entries[index];
            if (!entry.Enabled)
                return;

            MatchMenuAction(entry);
        }

        private void MatchMenuAction(FFMenuEntry entry)
        {
            switch (entry.Key)
            {
                case "attack":
                    ShowActionSubMenu();
                    break;

                case "items":
                    ShowItemSubMenu();
                    break;

                case "defend":
                    DoDefendAction();
                    break;

                case "escape":
                    DoEscapeAction();
                    break;

                case "action":
                    _currentMenuAction = entry;
                    var action = entry.Data as ActionCharacter;
                    if (action != null)
                    {
                        if (action.Type == ActionCharacterType.ToEnemy)
                            StartTargetSelect(false);
                        else if (action.Type == ActionCharacterType.ToAlias)
                            StartTargetSelect(true);
                        else if (action.Type == ActionCharacterType.ToSelf)
                        {
                            _targetSelectMode = false;
                            ExecuteMenuAction(_currentCharacter);
                        }
                    }
                    break;

                case "item":
                    _currentMenuAction = entry;
                    var item = entry.Data as IItem;
                    if (item != null)
                    {
                        if (item.ItemType.HasFlag(ItemType.TargetUsed))
                            StartTargetSelect(true);
                        else
                            DoItemAction(item);
                    }
                    break;

                case "back":
                    ShowMainMenu();
                    break;

                case "_empty":
                    break;
            }
        }

        private void ShowActionSubMenu()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return;

            var entries = new List<FFMenuEntry>();
            foreach (var action in _currentCharacter.Card.Actions)
            {
                if (!action.Type.UseInBattle())
                    continue;
                var cost = action.GetInt(ActionCharacter.ATTRIBUTE_REQUIRED_POINT, 1);
                var canUse = _currentCharacter.Card.Attributes.Point.Value >= cost;

                entries.Add(new FFMenuEntry
                {
                    Key = "action",
                    Label = $"{TranslationServer.Translate(action.Name)}  ({cost}P)",
                    Enabled = canUse,
                    Data = action
                });
            }

            if (entries.Count == 0)
            {
                entries.Add(new FFMenuEntry
                {
                    Key = "_empty",
                    Label = TranslationServer.Translate("ff_battle/no_actions"),
                    Enabled = false
                });
            }

            entries.Add(new FFMenuEntry
            {
                Key = "back",
                Label = TranslationServer.Translate("ff_battle/back"),
                Enabled = true
            });

            _menuPanel.ShowMenu(entries);
            _controller.State = FFBattleState.SubMenuOpen;
        }

        private void ShowItemSubMenu()
        {
            var inventory = GameData.Instance.Runtime.Inventory;
            var entries = new List<FFMenuEntry>();

            foreach (var container in inventory.Items)
            {
                var item = container.Item;
                if (item.ItemType.HasFlag(ItemType.Used) && container.Count > 0)
                {
                    entries.Add(new FFMenuEntry
                    {
                        Key = "item",
                        Label = $"{TranslationServer.Translate(item.Name)}  x{container.Count}",
                        Enabled = _currentCharacter.Card.Attributes.Point.Value >= 1,
                        Data = item
                    });
                }
            }

            if (entries.Count == 0)
            {
                entries.Add(new FFMenuEntry
                {
                    Key = "_empty",
                    Label = TranslationServer.Translate("ff_battle/no_items"),
                    Enabled = false
                });
            }

            entries.Add(new FFMenuEntry
            {
                Key = "back",
                Label = TranslationServer.Translate("ff_battle/back"),
                Enabled = true
            });

            _menuPanel.ShowMenu(entries);
            _controller.State = FFBattleState.SubMenuOpen;
        }

        private void StartTargetSelect(bool targetAlly)
        {
            _targetSelectMode = true;
            _targetAlly = targetAlly;
            _menuPanel.HideMenu();
            _controller.State = FFBattleState.TargetSelect;

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            if (targetAlly)
            {
                foreach (var widget in _playerPanel.Widgets)
                    if (!widget.IsDead)
                        widget.Highlight(new Color(0.3f, 1f, 0.3f, 0.5f));
            }
            else
            {
                foreach (var widget in _enemyPanel.Widgets)
                    if (!widget.IsDead)
                        widget.Highlight(new Color(1f, 0.3f, 0.3f, 0.5f));
            }

            _onTargetSelected = null;
        }

        private void OnEnemyClicked(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn || !_targetSelectMode || widget.IsDead)
                return;

            if (_targetAlly)
                return;

            _targetSelectMode = false;
            foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();

            ExecuteMenuAction(widget);
        }

        private void OnTargetSelected(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn || !_targetSelectMode || widget.IsDead)
                return;

            if (_targetAlly && _enemyPanel.Widgets.Contains(widget))
                return;

            if (!_targetAlly && _playerPanel.Widgets.Contains(widget))
                return;

            _targetSelectMode = false;
            foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();

            ExecuteMenuAction(widget);
        }

        private async void ExecuteMenuAction(FFCharacterWidget target)
        {
            if (_currentMenuAction == null)
                return;

            _controller.State = FFBattleState.AnimationPlaying;
            _endTurnButton.Visible = false;

            switch (_currentMenuAction.Key)
            {
                case "action":
                {
                    var action = _currentMenuAction.Data as ActionCharacter;
                    IBattleAction anim = null;

                    if (action.Type == ActionCharacterType.ToEnemy)
                        anim = new Actions.FFAttackAnimation(_currentCharacter, target, action);
                    else if (action.Type == ActionCharacterType.ToAlias)
                        anim = new Actions.FFDefendAnimation(_currentCharacter, target, action);
                    else if (action.Type == ActionCharacterType.ToSelf)
                        anim = new Actions.FFDefendAnimation(_currentCharacter, _currentCharacter, action);
                    else
                        GameLogger.Log("FF_BATTLE", $"Unknown action type: {action.Type}", LogLevel.Warning);

                    await RunAnimation(anim);
                    break;
                }

                case "item":
                {
                    var item = _currentMenuAction.Data as IItem;
                    var dummyAction = new ActionCharacter { ID = "ff/item" };
                    var anim = new Actions.FFItemAnimation(_currentCharacter, target, dummyAction, item);
                    await RunAnimation(anim);
                    break;
                }
            }

            _currentMenuAction = null;
            _endTurnButton.Visible = true;

            CheckEndCondition();
        }

        private void DoDefendAction()
        {
            var defendAction = _currentCharacter?.Card?.Actions
                ?.FirstOrDefault(a => a.Type == ActionCharacterType.ToAlias);
            if (defendAction == null)
                return;

            _currentMenuAction = new FFMenuEntry
            {
                Key = "action",
                Data = defendAction
            };

            _targetSelectMode = false;
            ExecuteMenuAction(_currentCharacter);
        }

        private void DoEscapeAction()
        {
            _menuPanel.HideMenu();
            _controller.State = FFBattleState.Idle;

            var escapeRoll = (float)GD.RandRange(0, 100);
            if (escapeRoll < 70f)
            {
                GameLogger.Log("FF_BATTLE", "Escape succeeded", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Player);
            }
            else
            {
                GameLogger.Log("FF_BATTLE", "Escape failed", LogLevel.Debug);
                _currentCharacter.Card.Attributes.Point.Value -= 1;
                _currentCharacter.UpdateInfo();
                _currentCharacter = null;
                TryNextCharacterOrEndTurn();
            }
        }

        private void DoItemAction(IItem item)
        {
            _currentMenuAction = new FFMenuEntry
            {
                Key = "item",
                Data = item
            };

            _targetSelectMode = false;
            ExecuteMenuAction(_currentCharacter);
        }

        private async Task RunAnimation(IBattleAction anim)
        {
            if (anim == null)
                return;

            var root = (Node)_ctx.RootNode;

            anim.DoStart();
            while (!anim.DoAction())
                await root.ToSignal(root.GetTree(), "process_frame");
            anim.DoEnd();
        }

        private void OnMenuCancel()
        {
            if (_targetSelectMode)
            {
                _targetSelectMode = false;
                foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
                foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
                ShowMainMenu();
                return;
            }

            if (_controller.State == FFBattleState.SubMenuOpen)
            {
                ShowMainMenu();
                return;
            }

            _menuPanel.HideMenu();
            _controller.State = FFBattleState.Idle;
            _currentCharacter = null;
            _currentMenuAction = null;
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
        }

        private void CheckEndCondition()
        {
            if (!_isPlayerTurn)
                return;

            if (!_enemyPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All enemies dead — player wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Player);
                return;
            }

            if (!_playerPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All players dead — enemy wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Enemy);
                return;
            }

            TryNextCharacterOrEndTurn();
        }

        private void TryNextCharacterOrEndTurn()
        {
            foreach (var widget in _playerPanel.Widgets)
            {
                if (widget.IsDead || widget.Card?.Attributes?.Point?.Value <= 0)
                    continue;

                if (widget.Card.StatusEffectQueue.HasStun())
                {
                    GameLogger.Log("FF_BATTLE", $"{widget.Card.Name} is stunned, skipping", LogLevel.Debug);
                    widget.Card.Attributes.Point.Value = 0;
                    widget.UpdateInfo();
                    continue;
                }

                _currentCharacter = widget;
                ShowMainMenu();
                return;
            }

            OnPlayerTurnEnd();
        }

        /// <summary>
        /// Ends the player's turn, hides all UI, and transitions to the enemy turn.
        /// Runs a brief delay before invoking <c>RunEnemyTurn()</c> which iterates
        /// over all alive enemies, each picking a random <see cref="ActionCharacter"/>
        /// and a random valid target, executing the action via the appropriate
        /// animation subclass. Checks for player defeat after each enemy action.
        /// If all enemies are dead, calls <see cref="IBattleContext.EndBattle"/>.
        /// Otherwise starts a new player turn.
        /// </summary>
        public async void OnPlayerTurnEnd()
        {
            GameLogger.Log("FF_BATTLE", "Player turn ended", LogLevel.Debug);

            _isPlayerTurn = false;
            _endTurnButton.Visible = false;
            _menuPanel.HideMenu();
            _controller.State = FFBattleState.AnimationPlaying;
            BattleSceneContext.Instance.IsWait = true;

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            var root = (Node)_ctx.RootNode;
            await root.ToSignal(root.GetTree(), "process_frame");
            await root.ToSignal(root.GetTree().CreateTimer(0.5f), "timeout");

            await RunEnemyTurn();
        }

        private async Task RunEnemyTurn()
        {
            GameLogger.Log("FF_BATTLE", "Enemy turn started", LogLevel.Debug);

            var root = (Node)_ctx.RootNode;

            foreach (var enemyWidget in _enemyPanel.Widgets.ToList())
            {
                if (enemyWidget.IsDead || enemyWidget.Card?.Actions == null || enemyWidget.Card.Actions.Count == 0)
                    continue;

                enemyWidget.Card.StatusEffectQueue.OnTurnStart();

                if (enemyWidget.Card.StatusEffectQueue.HasStun())
                {
                    GameLogger.Log("FF_BATTLE", $"{enemyWidget.Card.Name} is stunned, skipping", LogLevel.Debug);
                    continue;
                }

                enemyWidget.Card.Attributes.Point.Value = enemyWidget.Card.Attributes.Point.BaseValue;

                var action = PickEnemyAction(enemyWidget);
                if (action == null)
                    continue;

                var target = PickEnemyTarget(enemyWidget, action);
                if (target == null)
                    continue;

                GameLogger.Log("FF_BATTLE",
                    $"Enemy: {enemyWidget.Card.Name} → {action.Name} → {target.Card.Name}",
                    LogLevel.Debug);

                var anim = CreateEnemyAnimation(enemyWidget, target, action);
                if (anim != null)
                {
                    anim.DoStart();
                    while (!anim.DoAction())
                        await root.ToSignal(root.GetTree(), "process_frame");
                    anim.DoEnd();
                }

                await root.ToSignal(root.GetTree().CreateTimer(0.2f), "timeout");

                if (!_playerPanel.HasAlive())
                {
                    GameLogger.Log("FF_BATTLE", "All players dead — enemy wins", LogLevel.Debug);
                    _ctx.EndBattle(BattleSide.Enemy);
                    return;
                }
            }

            if (!_playerPanel.HasAlive())
            {
                _ctx.EndBattle(BattleSide.Enemy);
                return;
            }

            StartPlayerTurn();
        }

        private ActionCharacter PickEnemyAction(FFCharacterWidget widget)
        {
            var actions = widget.Card.Actions;
            if (actions == null || actions.Count == 0)
                return null;

            var random = new Random();
            return actions[random.Next(actions.Count)];
        }

        private FFCharacterWidget PickEnemyTarget(FFCharacterWidget source, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
            {
                var alivePlayers = _playerPanel.Widgets
                    .Where(w => !w.IsDead && w.Card?.Attributes?.Health?.Value > 0)
                    .ToList();

                if (alivePlayers.Count == 0)
                    return null;
                return alivePlayers[new Random().Next(alivePlayers.Count)];
            }

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return source;

            return null;
        }

        private Actions.FFAbstractAnimation CreateEnemyAnimation(
            FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
                return new Actions.FFAttackAnimation(source, target, action, 0.8f);

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return new Actions.FFDefendAnimation(source, target, action, 0.8f);

            GameLogger.Log("FF_BATTLE", $"Unknown action type for enemy: {action.Type}", LogLevel.Warning);
            return null;
        }

        private void OnEndTurnPressed()
        {
            if (!_isPlayerTurn)
                return;

            OnPlayerTurnEnd();
        }

        private void OnEnemyDied(FFCharacterWidget widget)
        {
            var ctx = BattleSceneContext.Instance;
            if (ctx != null && widget?.Card != null)
            {
                ctx.RecordEnemyDefeated(widget.Card);
                ctx.NotifyCharacterDied(widget.Card);
            }

            if (!_enemyPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All enemies dead — player wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Player);
            }
        }

        private bool HasAvailableActions()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return false;

            foreach (var action in _currentCharacter.Card.Actions)
            {
                if (!action.Type.UseInBattle())
                    continue;
                var cost = action.GetInt(ActionCharacter.ATTRIBUTE_REQUIRED_POINT, 1);
                if (_currentCharacter.Card.Attributes.Point.Value >= cost)
                    return true;
            }
            return false;
        }

        private bool HasUsableItems()
        {
            var inventory = GameData.Instance.Runtime.Inventory;
            foreach (var container in inventory.Items)
            {
                if (container.Item.ItemType.HasFlag(ItemType.Used) && container.Count > 0)
                    return true;
            }
            return false;
        }

        private bool HasDefendAction()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return false;

            return _currentCharacter.Card.Actions.Any(a =>
                a.Type == ActionCharacterType.ToAlias
                && _currentCharacter.Card.Attributes.Point.Value >= a.GetInt(ActionCharacter.ATTRIBUTE_REQUIRED_POINT, 1));
        }

        private void OnBattleCleared()
        {
            _playerPanel?.ClearAll();
            _enemyPanel?.ClearAll();
            _menuPanel?.HideMenu();
            if (_endTurnButton != null)
                _endTurnButton.Visible = false;
        }
    }
}
