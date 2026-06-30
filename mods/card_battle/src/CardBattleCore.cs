using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;
using Cthangover.Core.Characters;
using Cthangover.CardBattle.Player;
using Cthangover.CardBattle.UI;
using Godot;

namespace Cthangover.CardBattle
{
    /// <summary>
    /// Core orchestrator for the card-based turn battle system.
    /// Manages the full battle lifecycle: initializing UI panels (<see cref="BattleCardPanel"/> and <see cref="CardController"/>),
    /// cycling between player and enemy turns, spawning animated actions via <see cref="AbstractBattleAction"/>,
    /// applying status effects each turn, and detecting win/loss conditions when all cards on one side are dead.
    /// Implements <see cref="IBattleCore"/> so it can be registered as a battle type provider in the core battle engine.
    /// </summary>
    public class CardBattleCore : IBattleCore
    {
        /// <summary>
        /// Unique identifier for this battle core, used by the battle engine to select the card battle mod.
        /// </summary>
        public string Id => "card_battle";

        /// <summary>
        /// Provides <see cref="IActionExecutor"/> instances for the three card action types
        /// (physics/attack, physics/defence, physics/stun) used during action resolution.
        /// </summary>
        public IActionExecutorProvider ActionProvider { get; } = new Actions.CardBattleActionProvider();

        private Character[] _playerChars;
        private Character[] _enemyChars;
        private IBattleContext _ctx;

        private BattleCardPanel _playerPanel;
        private BattleCardPanel _enemyPanel;
        private CardController _cardController;
        private Control _actionPanel;
        private Control _toolPanel;
        private EndTurnButton _endTurnButton;

        private BattleSide _currentOrder;

        private const float CELL_OFFSET = 10f;
        private const int PLAYER_CARD_LIMIT = 4;
        private const float BASE_CARD_WIDTH = 230f;
        private const float BASE_CARD_HEIGHT = 420f;

        /// <summary>
        /// Initializes the card battle by constructing the player and enemy <see cref="BattleCardPanel"/> instances,
        /// the <see cref="CardController"/> for drag-and-drop input, and the <see cref="EndTurnButton"/>.
        /// Enemy characters are deep-copied so defeats are tracked independently per battle instance.
        /// The <see cref="CardBattleActionProvider"/> is registered with <see cref="ActionExecutorHub"/>.
        /// </summary>
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

            _playerPanel = new BattleCardPanel { Name = "PlayerPanel" };
            _playerPanel.EnsureConstructed();
            panel.AddChild(_playerPanel);

            _enemyPanel = new BattleCardPanel { Name = "EnemyPanel", AlignType = "Right" };
            _enemyPanel.EnsureConstructed();
            _enemyPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.AddChild(_enemyPanel);

            _actionPanel = new Control { Name = "ActionPanel" };
            panel.AddChild(_actionPanel);

            _toolPanel = root.GetNodeOrNull<Control>("ToolPanel");

            _endTurnButton = new EndTurnButton { Name = "EndTurnButton" };
            _endTurnButton.EnsureConstructed();
            _endTurnButton.SetVisible(false);
            _endTurnButton.OnPressed += OnEndTurnPressed;
            _toolPanel?.AddChild(_endTurnButton);

            _cardController = new CardController { Name = "CardController" };
            panel.AddChild(_cardController);
            _cardController.FindPanels();
        }

        /// <summary>
        /// Positions the UI panels relative to the viewport, calculates a unified <c>cardScale</c> that
        /// fits both player and enemy cards on screen, initializes all character cards, and starts the first player turn.
        /// Subscribes to <c>OnCardDead</c> to record defeated enemies and to <c>OnActionExecuted</c> to detect
        /// automatic turn completion when all player cards exhaust their action points.
        /// </summary>
        public void Start()
        {
            var root = _ctx.RootNode as Node;

            var panelSize = root.GetViewport().GetVisibleRect().Size;
            var enemyCount = _enemyChars?.Length ?? 1;

            _enemyPanel.UseRows = enemyCount > 6;

            _playerPanel.Position = new Vector2(0, panelSize.Y * 0.55f);
            _playerPanel.Size = new Vector2(panelSize.X * 0.55f, panelSize.Y * 0.35f);

            _enemyPanel.Position = new Vector2(panelSize.X * 0.02f, panelSize.Y * 0.022f);
            _enemyPanel.Size = new Vector2(panelSize.X * 0.96f, panelSize.Y * 0.446f);

            _actionPanel.Position = new Vector2(panelSize.X * 0.53f, panelSize.Y * 0.55f);
            _actionPanel.Size = new Vector2(panelSize.X * 0.46f, panelSize.Y * 0.35f);

            if (_endTurnButton != null)
                _endTurnButton.Position = new Vector2(panelSize.X - 180f, (panelSize.Y * 0.1f - 50f) / 2);

            var cardScale = CalculateCardScale(enemyCount);
            _cardController.CardScale = cardScale;

            _playerPanel.Init(_playerChars, true, cardScale);
            _enemyPanel.Init(_enemyChars, false, cardScale);

            _enemyPanel.OnCardDead += OnEnemyCardDead;
            _cardController.OnActionExecuted += CheckPlayerTurnEnd;

            BattleSceneContext.Instance.OnBattleCleared += OnBattleCleared;

            _currentOrder = BattleSide.Player;
            StartPlayerTurn();
        }

        private float CalculateCardScale(int enemyCount)
        {
            var viewport = ((Node)_ctx.RootNode).GetViewport().GetVisibleRect().Size;
            float enemyPanelWidth = viewport.X * 0.96f;
            float playerPanelWidth = viewport.X * 0.55f;
            float enemyPanelHeight = viewport.Y * 0.446f;

            float maxCardScale = 1f;

            if (_enemyPanel.UseRows)
            {
                int maxPerRow = _enemyPanel.GetMaxCardsPerRow();
                int rows = _enemyPanel.GetCurrentRowCount(enemyCount);

                float widthScale = (enemyPanelWidth - (maxPerRow - 1) * CELL_OFFSET) / (maxPerRow * BASE_CARD_WIDTH);
                float heightScale = (enemyPanelHeight - (rows - 1) * CELL_OFFSET) / (rows * BASE_CARD_HEIGHT);
                maxCardScale = Mathf.Max(0.1f, Mathf.Min(widthScale, heightScale));
            }
            else
            {
                int effectiveCount = Mathf.Max(enemyCount, 1);
                maxCardScale = Mathf.Max(0.1f, (enemyPanelWidth - (effectiveCount - 1) * CELL_OFFSET) / (effectiveCount * BASE_CARD_WIDTH));
            }

            float playerFitScale = Mathf.Max(0.1f, (playerPanelWidth - (PLAYER_CARD_LIMIT - 1) * CELL_OFFSET) / (PLAYER_CARD_LIMIT * BASE_CARD_WIDTH));

            float cardScale = Mathf.Min(maxCardScale, playerFitScale);
            return Mathf.Min(cardScale, 1f);
        }

        private void OnEnemyCardDead(CharacterCardNode card)
        {
            if (card?.Card == null)
                return;

            var ctx = BattleSceneContext.Instance;
            if (ctx != null)
            {
                ctx.RecordEnemyDefeated(card.Card);
                ctx.NotifyCharacterDied(card.Card);
            }
        }

        private void StartPlayerTurn()
        {
            GameLogger.Log("CARD_BATTLE", "Player turn started", LogLevel.Debug);

            _currentOrder = BattleSide.Player;
            BattleSceneContext.Instance.IsWait = false;

            _cardController.ClearSelections();
            _cardController.ClearActions();

            _endTurnButton?.SetVisible(true);

            foreach (var card in _playerPanel.CardList)
            {
                card.Card.StatusEffectQueue.OnTurnStart();
                card.Card.Attributes.Point.Value = card.Card.Attributes.Point.BaseValue;
                card.UpdateInfo();
            }
        }

        private void OnEndTurnPressed()
        {
            GameLogger.Log("CARD_BATTLE", "End turn button pressed", LogLevel.Debug);
            OnPlayerTurnEnd();
        }

        private void CheckPlayerTurnEnd()
        {
            if (_currentOrder != BattleSide.Player)
                return;

            if (!HasAliveCards(_enemyPanel))
            {
                _ctx.EndBattle(BattleSide.Player);
                return;
            }

            bool allOutOfPoints = true;
            foreach (var card in _playerPanel.CardList)
            {
                if (!card.IsDead && card.Card?.Attributes?.Point?.Value > 0)
                {
                    allOutOfPoints = false;
                    break;
                }
            }

            if (allOutOfPoints)
                OnPlayerTurnEnd();
        }

        /// <summary>
        /// Ends the player's turn by hiding the end-turn button, setting the battle to <c>IsWait</c>,
        /// and yielding two frames for UI to settle before running the enemy AI turn via <c>RunEnemyTurn</c>.
        /// If all enemies are already dead, the battle ends immediately with the player as winner.
        /// This method is <c>async void</c> so it can await frames without blocking the game loop.
        /// </summary>
        public async void OnPlayerTurnEnd()
        {
            GameLogger.Log("CARD_BATTLE", "Player turn ended", LogLevel.Debug);
            
            _endTurnButton?.SetVisible(false);
            BattleSceneContext.Instance.IsWait = true;

            if (!HasAliveCards(_enemyPanel))
            {
                _ctx.EndBattle(BattleSide.Player);
                return;
            }

            var root = (Node)_ctx.RootNode;
            await root.ToSignal(root.GetTree(), "process_frame");
            await root.ToSignal(root.GetTree().CreateTimer(0.5f), "timeout");

            await RunEnemyTurn();
        }

        private async Task RunEnemyTurn()
        {
            GameLogger.Log("CARD_BATTLE", "Enemy turn started", LogLevel.Debug);

            _currentOrder = BattleSide.Enemy;
            _endTurnButton?.SetVisible(false);

            var root = (Node)_ctx.RootNode;

            foreach (var card in _enemyPanel.CardList.ToList())
            {
                if (card.IsDead || card.Card?.Actions == null || card.Card.Actions.Count == 0)
                    continue;

                card.Card.StatusEffectQueue.OnTurnStart();

                if (card.Card.StatusEffectQueue.HasStun())
                {
                    GameLogger.Log("CARD_BATTLE", $"{card.Card.Name} is stunned, skipping", LogLevel.Debug);
                    continue;
                }

                card.Card.Attributes.Point.Value = card.Card.Attributes.Point.BaseValue;

                var action = PickEnemyAction(card);
                if (action == null)
                    continue;

                var target = PickEnemyTarget(card, action);
                if (target == null)
                    continue;

                GameLogger.Log("CARD_BATTLE",
                    $"Enemy action: {card.Card.Name} -> {action.Name} -> {target.Card.Name}",
                    LogLevel.Message);

                var animatedAction = CreateAnimatedAction(card, target, action);
                if (animatedAction != null)
                {
                    animatedAction.DoStart();
                    while (!animatedAction.DoAction())
                        await root.ToSignal(root.GetTree(), "process_frame");
                    animatedAction.DoEnd();
                }

                await root.ToSignal(root.GetTree().CreateTimer(0.2f), "timeout");

                if (!HasAliveCards(_playerPanel))
                {
                    GameLogger.Log("CARD_BATTLE", "All players dead — enemy wins", LogLevel.Message);
                    _ctx.EndBattle(BattleSide.Enemy);
                    return;
                }
            }

            if (!HasAliveCards(_playerPanel))
            {
                _ctx.EndBattle(BattleSide.Enemy);
                return;
            }

            StartPlayerTurn();
        }

        private ActionCharacter PickEnemyAction(CharacterCardNode card)
        {
            var actions = card.Card.Actions;
            if (actions == null || actions.Count == 0)
                return null;

            var random = new Random();
            return actions[random.Next(actions.Count)];
        }

        private CharacterCardNode PickEnemyTarget(CharacterCardNode source, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
            {
                var alivePlayers = _playerPanel.CardList
                    .Where(c => !c.IsDead && c.Card?.Attributes?.Health?.Value > 0)
                    .ToList();

                if (alivePlayers.Count == 0)
                    return null;
                return alivePlayers[new Random().Next(alivePlayers.Count)];
            }

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return source;

            GameLogger.Log("CARD_BATTLE", $"PickEnemyTarget: unsupported action type {action.Type}", LogLevel.Warning);
            return null;
        }

        private Actions.AbstractBattleAction CreateAnimatedAction(CharacterCardNode source, CharacterCardNode target, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
                return new Actions.PhysicsAttackAction(source, target, action);

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return new Actions.PhysicsDefenceAction(source, target, action);

            GameLogger.Log("CARD_BATTLE", $"CreateAnimatedAction: unsupported action type {action.Type}", LogLevel.Warning);
            return null;
        }

        private bool HasAliveCards(BattleCardPanel panel)
        {
            foreach (var card in panel.CardList)
            {
                if (!card.IsDead && card.Card?.Attributes?.Health?.Value > 0)
                    return true;
            }
            return false;
        }

        private void OnBattleCleared()
        {
            _playerPanel?.ClearAll();
            _enemyPanel?.ClearAll();
            _cardController?.ClearActions();
            _endTurnButton?.SetVisible(false);
        }
    }
}
