using System.Collections.Generic;
using Cthangover.Core.Cards;
using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    public partial class CardController : InputHandlerNode
    {
        private Control actionPanel;
        private BattleCardPanel[] panels;

        private List<CharacterCardNode> characterCards = new();
        private List<ActionCardNode> actionCards = new();

        private CharacterCardNode selectedCharacter;
        private CharacterCardNode targetCharacter;
        private ActionCardNode currentActionCharacter;

        private bool isDragging;
        private bool dragValid;
        private float dragThreshold = 15f;
        private Vector2 pressPosition;
        private Node _originalActionCardParent;

        public void FindPanels()
        {
            actionPanel = GetNodeOrNull<Control>("../ActionPanel");
            panels = new[]
            {
                GetNodeOrNull<BattleCardPanel>("../PlayerPanel"),
                GetNodeOrNull<BattleCardPanel>("../EnemyPanel"),
            };
        }

        protected override void OnInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb1 && mb1.Pressed && mb1.ButtonIndex == MouseButton.Left)
                GameLogger.Log("CARD_CTRL", "_Input received mouse press", LogLevel.Debug);

            if (Cthangover.Core.Battle.BattleSceneContext.Instance != null &&
                (Cthangover.Core.Battle.BattleSceneContext.Instance.IsWait ||
                 Cthangover.Core.Battle.BattleSceneContext.Instance.IsDestroyed))
            {
                if (@event is InputEventMouseButton mb2 && !mb2.Pressed)
                    GameLogger.Log("CARD_CTRL",
                        $"Input blocked: IsWait={Cthangover.Core.Battle.BattleSceneContext.Instance.IsWait} IsDestroyed={Cthangover.Core.Battle.BattleSceneContext.Instance.IsDestroyed}",
                        LogLevel.Debug);
                return;
            }

            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    if (mouseButton.Pressed)
                    {
                        pressPosition = mouseButton.GlobalPosition;
                        isDragging = false;
                        dragValid = false;
                        OnBeginDrag(mouseButton.GlobalPosition);
                    }
                    else
                    {
                        if (isDragging && dragValid)
                            OnEndDrag(mouseButton.GlobalPosition);
                        else
                            OnPointerClick(mouseButton.GlobalPosition);
                        isDragging = false;
                        dragValid = false;
                    }
                }
            }

            if (@event is InputEventMouseMotion mouseMotion)
            {
                if (mouseMotion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    if (dragValid && !isDragging && (mouseMotion.GlobalPosition - pressPosition).Length() > dragThreshold)
                        isDragging = true;
                    if (isDragging && dragValid)
                        OnDrag(mouseMotion.GlobalPosition);
                }
            }
        }

        private float cardScale = 1f;

        public float CardScale
        {
            get => cardScale;
            set => cardScale = value;
        }

        public event System.Action OnActionExecuted;

        public void ShowActions(CharacterCardNode character)
        {
            if (character?.Card?.Actions == null)
            {
                GameLogger.Log("CARD_CTRL",
                    $"ShowActions: NULL actions for character {(character?.Card?.Name ?? "null")}",
                    LogLevel.Error);
                return;
            }

            GameLogger.Log("CARD_CTRL",
                $"ShowActions: {character.Card.Name} ({character.Card.Actions.Count} actions)",
                LogLevel.Debug);

            DiscardActionCharacters();

            foreach (var action in character.Card.Actions)
            {
                var cardNode = CreateAction(action, cardScale);
                cardNode.Modulate = new Color(1, 1, 1, 0);
                actionCards.Add(cardNode);

                var tween = CreateTween();
                tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(cardNode, "modulate:a", 1f, 0.25f);
            }

            Redraw(animate: false);
        }

        public void DiscardActionCharacters()
        {
            if (actionCards.Count == 0)
                return;

            var oldCards = new List<ActionCardNode>(actionCards);
            actionCards.Clear();

            foreach (var card in oldCards)
            {
                if (!GodotObject.IsInstanceValid(card))
                    continue;

                card.MouseFilter = Control.MouseFilterEnum.Ignore;
                var tween = CreateTween();
                tween.SetParallel(true);
                tween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(card, "modulate:a", 0f, 0.15f);
                tween.TweenProperty(card, "position:y", card.Position.Y + 40f, 0.15f);
                tween.Chain().TweenCallback(Callable.From(card.QueueFree));
            }
        }

        public void ClearSelections(bool clearSource = true)
        {
            if (currentActionCharacter != null)
            {
                currentActionCharacter.Unselect();
                currentActionCharacter = null;
            }
            if (selectedCharacter != null && clearSource)
            {
                selectedCharacter.Unselect();
                selectedCharacter = null;
                DiscardActionCharacters();
            }
            if (targetCharacter != null)
            {
                targetCharacter.Unselect();
                targetCharacter = null;
            }

            if (panels != null)
            {
                foreach (var panel in panels)
                    panel?.Redraw();
            }
        }

        public void ClearActions()
        {
            if (actionCards.Count > 0)
            {
                foreach (var card in actionCards)
                    card.QueueFree();
                actionCards.Clear();
            }
        }

        public void Redraw(bool animate = true)
        {
            var size = actionCards.Count;
            for (int i = 0; i < size; i++)
                SetActionCharacterPosition(actionCards[i], i, size, animate);
        }

        private void SetActionCharacterPosition(ActionCardNode card, int index, int count, bool animate)
        {
            var rect = card.GetControlNode();
            var targetPos = new Vector2(rect.Size.X * index, 0);

            if (animate)
            {
                var tween = CreateTween();
                tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(rect, "position", targetPos, 0.25f);
            }
            else
            {
                rect.Position = targetPos;
            }
        }

        public ActionCardNode CreateAction(ActionCharacter actionCard, float cardScale)
        {
            var card = new ActionCardNode();
            card.EnsureConstructed();
            actionPanel.AddChild(card);
            card.Scale = new Vector2(cardScale, cardScale);
            card.Init(actionCard);
            return card;
        }

        public void DoActionToTarget()
        {
            var strategy = Cthangover.CardBattle.Player.CardActionStrategyFactory.Get(
                currentActionCharacter.Card, selectedCharacter, targetCharacter);
            strategy?.Execute(selectedCharacter, targetCharacter, currentActionCharacter.Card);
        }

        private void OnPointerClick(Vector2 screenPosition)
        {
            characterCards = GetAllCharacters();
            var source = TryFindCardByPosition(screenPosition, characterCards);

            if (source != null)
                GameLogger.Log("CARD_CTRL",
                    $"Click on card: {source.Card.Name} isPlayer={source.IsPlayer} actionsCount={source.Card.Actions?.Count ?? 0}",
                    LogLevel.Debug);
            else
                GameLogger.Log("CARD_CTRL", "Click on empty space", LogLevel.Debug);

            if (source != selectedCharacter && selectedCharacter != null)
                selectedCharacter.Unselect();

            if (source != null && source.IsPlayer)
            {
                var audioService = SceneContextNode.Instance?.GetSceneRoot<AudioService>("AudioService");
                audioService?.PlaySound("battle/card_show_actions", Cthangover.Core.Audio.SoundType.CardEffect);
                selectedCharacter = source;
                ShowActions(selectedCharacter);
                selectedCharacter.Select();
            }
            else
            {
                ClearSelections();
            }
        }

        private void OnBeginDrag(Vector2 screenPosition)
        {
            var source = TryFindCardByPosition(screenPosition, actionCards);
            if (currentActionCharacter != null && source != currentActionCharacter)
                currentActionCharacter.Unselect();

            if (source != null)
            {
                dragValid = true;
                var audioService = SceneContextNode.Instance?.GetSceneRoot<AudioService>("AudioService");
                audioService?.PlaySound("battle/card_pick", 4, Cthangover.Core.Audio.SoundType.CardEffect);

                GameLogger.Log("CARD_CTRL",
                    $"Begin drag: {source.Card.Name}",
                    LogLevel.Debug);

                currentActionCharacter = source;
                var control = currentActionCharacter.GetControlNode();
                _originalActionCardParent = control.GetParent();
                control.Reparent(GetTree().Root);
                control.ZIndex = 4096;
                var pickTween = CreateTween();
                pickTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                pickTween.TweenProperty(control, "scale", new Vector2(1.1f, 1.1f), 0.1f);

                foreach (var card in actionCards)
                {
                    if (card != currentActionCharacter && GodotObject.IsInstanceValid(card))
                        card.MouseFilter = Control.MouseFilterEnum.Ignore;
                }
            }
        }

        private void OnDrag(Vector2 screenPosition)
        {
            if (currentActionCharacter == null)
                return;

            var cardControl = currentActionCharacter.GetControlNode();
            cardControl.GlobalPosition = screenPosition - cardControl.Size / 2;

            var target = TryFindCardByRect(cardControl, characterCards);

            if (target != targetCharacter && targetCharacter != null)
                targetCharacter.Unselect();

            if (target != null)
            {
                targetCharacter = target;
                var strategy = Cthangover.CardBattle.Player.CardActionStrategyFactory.Get(
                    currentActionCharacter.Card, selectedCharacter, target);

                var valid = strategy != null && strategy.Check(currentActionCharacter.Card, selectedCharacter, target);

                GameLogger.Log("CARD_CTRL",
                    $"Drag over: {target.Card.Name} valid={valid}",
                    LogLevel.Debug);

                if (valid)
                {
                    strategy.HighlightTarget(currentActionCharacter, targetCharacter);
                }
                else
                {
                    targetCharacter.Invalid();
                    currentActionCharacter.Invalid();
                }
            }
            else
            {
                targetCharacter = null;
                currentActionCharacter.Unselect();
            }
        }

        private void OnEndDrag(Vector2 screenPosition)
        {
            foreach (var card in actionCards)
            {
                if (card != null && GodotObject.IsInstanceValid(card))
                    card.MouseFilter = Control.MouseFilterEnum.Stop;
            }

            if (selectedCharacter != null && currentActionCharacter != null && targetCharacter != null)
            {
                var strategy = Cthangover.CardBattle.Player.CardActionStrategyFactory.Get(
                    currentActionCharacter.Card, selectedCharacter, targetCharacter);

                var valid = strategy != null && strategy.Check(currentActionCharacter.Card, selectedCharacter, targetCharacter);

                GameLogger.Log("CARD_CTRL",
                    $"End drag: {currentActionCharacter.Card.Name} from {selectedCharacter.Card.Name} to {targetCharacter.Card.Name} valid={valid}",
                    LogLevel.Debug);

                if (valid)
                {
                    if (_originalActionCardParent != null)
                    {
                        currentActionCharacter.GetControlNode().Reparent(_originalActionCardParent);
                        _originalActionCardParent = null;
                    }

                    var audioService = SceneContextNode.Instance?.GetSceneRoot<AudioService>("AudioService");
                    audioService?.PlaySound("battle/card_drop", 4, Cthangover.Core.Audio.SoundType.CardEffect);

                    DoActionToTarget();

                    if (panels != null)
                    {
                        foreach (var panel in panels)
                            panel?.Redraw();
                    }

                    ClearSelections();
                    OnActionExecuted?.Invoke();
                    return;
                }
            }

            if (currentActionCharacter != null)
            {
                var control = currentActionCharacter.GetControlNode();
                if (_originalActionCardParent != null)
                {
                    control.Reparent(_originalActionCardParent);
                    _originalActionCardParent = null;
                }
                var resetTween = CreateTween();
                resetTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                resetTween.TweenProperty(control, "scale", Vector2.One, 0.15f);
                control.ZIndex = 0;
            }

            Redraw();
            ClearSelections(false);
        }

        private T TryFindCardByPosition<T>(Vector2 screenPosition, IList<T> cards, ICard exclude = null) where T : class, ICard
        {
            if (cards == null)
                return null;
            foreach (var card in cards)
            {
                if (card == exclude || card == null)
                    continue;
                if (card.Frame != null && card.Frame.GetGlobalRect().HasPoint(screenPosition))
                    return card;
            }
            return null;
        }

        private T TryFindCardByRect<T>(Control draggingRect, IList<T> cards, ICard exclude = null) where T : class, ICard
        {
            Vector2 screenPoint = draggingRect.GlobalPosition + draggingRect.Size / 2;
            foreach (var card in cards)
            {
                if (card == exclude || card == null)
                    continue;
                if (card.Frame != null && card.Frame.GetGlobalRect().HasPoint(screenPoint))
                    return card;
            }
            return null;
        }

        private List<CharacterCardNode> GetAllCharacters()
        {
            var list = new List<CharacterCardNode>();
            var root = GetParent();
            if (root != null)
                CollectNodes<CharacterCardNode>(root, list);
            return list;
        }

        private static void CollectNodes<T>(Node parent, List<T> results) where T : Node
        {
            foreach (Node child in parent.GetChildren())
            {
                if (child is T t)
                    results.Add(t);
                CollectNodes(child, results);
            }
        }
    }
}
