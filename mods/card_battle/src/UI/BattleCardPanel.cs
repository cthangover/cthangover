using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    /// <summary>
    /// Container panel that manages a list of <see cref="CharacterCardNode"/> instances for one side
    /// of the battle (player or enemy). Handles card creation, multi-row layout when enemy count exceeds
    /// <c>MAX_CARDS_ROW</c> (10 per row), smooth animated repositioning, health-triggered death,
    /// and the <see cref="CardDeathAnimation"/> dissolve sequence.
    /// Two instances are created by <see cref="CardBattleCore.Init"/>: a left-aligned player panel
    /// and a right-aligned enemy panel.
    /// </summary>
    public partial class BattleCardPanel : ModWidget
    {
        private const int MAX_CARDS_ROW = 10;
        private const float BASE_CARD_HEIGHT = 280f;

        [Export] private Control content;
        /// <summary>
        /// Controls card alignment within the panel. <c>"Left"</c> for the player side,
        /// <c>"Right"</c> for the enemy side. Affects <see cref="CalcTargetPosition"/>.
        /// </summary>
        [Export] public string AlignType { get; set; } = "Left";
        [Export] private float cellOffset = 10f;

        /// <summary>
        /// All character cards currently managed by this panel. Populated by <see cref="Init"/>
        /// and modified by <see cref="Dead"/> (removal) and <see cref="ClearAll"/> (full clear).
        /// </summary>
        public List<CharacterCardNode> CardList { get; } = new();
        private Dictionary<CharacterCardNode, Tween> activeTweens = new();

        /// <summary>
        /// When <c>true</c>, cards are laid out in multiple rows (up to 3) instead of a single row.
        /// Set by <see cref="CardBattleCore.Start"/> when enemy count exceeds 6.
        /// </summary>
        public bool UseRows { get; set; }

        /// <summary>
        /// Fired when a card's death animation completes. <see cref="CardBattleCore"/> subscribes
        /// to record the defeated character in <see cref="BattleSceneContext"/>.
        /// </summary>
        public event System.Action<CharacterCardNode> OnCardDead;

        protected override void Construct() { }

        /// <summary>
        /// Clears the current card list and creates new <see cref="CharacterCardNode"/> instances
        /// for each <paramref name="cards"/> entry. Sets player/enemy team colors and subscribes
        /// the health-change handler. Returns the created card list.
        /// Called by <see cref="CardBattleCore.Start"/> with the full character roster for each side.
        /// </summary>
        public List<CharacterCardNode> Init(IEnumerable<Character> cards, bool isPlayer, float cardScale)
        {
            CardList.Clear();
            activeTweens.Clear();

            int idx = 0;
            foreach (var card in cards)
            {
                CardList.Add(Create(card, isPlayer, cardScale));
                idx++;
            }
            Redraw(animate: false);

            return CardList;
        }

        /// <summary>
        /// Instantiates a single <see cref="CharacterCardNode"/>, adds it to the content panel,
        /// sets its scale, initializes it with card data, applies team colors, and hooks
        /// the health-change event. Called by <see cref="Init"/> for each character.
        /// </summary>
        public CharacterCardNode Create(Character card, bool isPlayer, float cardScale)
        {
            var behaviour = new CharacterCardNode();
            behaviour.EnsureConstructed();

            if (content != null)
                content.AddChild(behaviour);
            else
                AddChild(behaviour);

            behaviour.Scale = new Vector2(cardScale, cardScale);

            behaviour.Init(card);
            behaviour.IsPlayer = isPlayer;
            behaviour.SetTeamColors();

            if (card != null)
                card.Attributes.Health.OnChange += CheckHealthAttribute;

            return behaviour;
        }

		private void CheckHealthAttribute(float value, float basevalue)
		{
			if (value > 0)
				return;

			foreach (var cardItem in CardList.ToList())
			{
				if (cardItem.Card.Attributes.Health.Value <= 0)
					Dead(cardItem);
			}
		}

		/// <summary>
		/// Frees all card nodes from the scene tree, clears the card list, and triggers a redraw.
		/// Called by <see cref="CardBattleCore.OnBattleCleared"/> when the battle scene is cleaned up.
		/// </summary>
		public void ClearAll()
		{
			foreach (var card in CardList.ToList())
			{
				if (GodotObject.IsInstanceValid(card))
					card.QueueFree();
			}
			CardList.Clear();
			activeTweens.Clear();
			Redraw(animate: false);
		}

		/// <summary>
		/// Recalculates and applies positions for all cards in the list. When <paramref name="animate"/>
		/// is <c>true</c>, cards tween to their new positions with a cubic ease-out; otherwise they
		/// snap instantly. Called after cards are added, removed, or when a selection/unselection
		/// changes the sort order.
		/// </summary>
		public void Redraw(bool animate = true)
        {
            for (int i = 0; i < CardList.Count; i++)
            {
                var card = CardList[i];
                if (card == null)
                    continue;

                var (row, col) = GetRowCol(i);
                SetCardPosition(card, i, row, col, animate);
            }
        }

        private (int row, int col) GetRowCol(int index)
        {
            if (!UseRows || index < MAX_CARDS_ROW)
                return (0, index);

            if (index < MAX_CARDS_ROW * 2)
                return (1, index - MAX_CARDS_ROW);

            return (2, index - MAX_CARDS_ROW * 2);
        }

        private int CalcBaseZIndex(int row, int col)
        {
            return row * 20 + col;
        }

        private Vector2 CalcTargetPosition(CharacterCardNode card, int row, int col)
        {
            var panelSize = Size;
            float effectiveWidth = card.Size.X * card.Scale.X;
            float effectiveHeight = card.Size.Y * card.Scale.Y;

            float y = row * (effectiveHeight + cellOffset);

            if (AlignType == "Left")
                return new Vector2((cellOffset + effectiveWidth) * col, y);
            else
                return new Vector2(panelSize.X - (cellOffset + effectiveWidth) * (col + 1), y);
        }

        /// <summary>
        /// Calculates how many rows are needed to display <paramref name="enemyCount"/> enemies
        /// when <see cref="UseRows"/> is enabled. Returns 1, 2, or 3.
        /// Called by <see cref="CardBattleCore.CalculateCardScale"/> to determine maximum card scale.
        /// </summary>
        public int GetCurrentRowCount(int enemyCount)
        {
            if (!UseRows)
                return 1;

            if (enemyCount <= MAX_CARDS_ROW)
                return 1;
            if (enemyCount <= MAX_CARDS_ROW * 2)
                return 2;
            return 3;
        }

        /// <summary>
        /// Returns the maximum cards per row constant (<c>MAX_CARDS_ROW = 10</c>).
        /// Used by <see cref="CardBattleCore.CalculateCardScale"/> for scale calculations.
        /// </summary>
        public int GetMaxCardsPerRow()
        {
            return MAX_CARDS_ROW;
        }

        private void SetCardPosition(CharacterCardNode card, int index, int row, int col, bool animate)
        {
            if (card == null)
                return;

            var targetPos = CalcTargetPosition(card, row, col);

            if (activeTweens.TryGetValue(card, out var prevTween) && prevTween != null && prevTween.IsValid())
                prevTween.Kill();

            if (animate)
            {
                var tween = CreateTween();
                tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(card, "position", targetPos, 0.3f);
                activeTweens[card] = tween;
            }
            else
            {
                card.Position = targetPos;
                activeTweens.Remove(card);
            }

            var baseZ = CalcBaseZIndex(row, col);
            card.SetBaseZIndex(baseZ);

            MoveChild(card, index);
            card.UpdateInfo();
        }

        /// <summary>
        /// Marks the card as dead and starts the <see cref="CardDeathAnimation"/> dissolve sequence.
        /// Once the animation completes, the card is removed from <see cref="CardList"/>,
        /// <see cref="OnCardDead"/> is fired, and the remaining cards are redrawn.
        /// Triggered by the health-change handler when a character's health reaches zero.
        /// </summary>
        public void Dead(CharacterCardNode characterCardNode)
        {
            if (characterCardNode == null || characterCardNode.IsDead)
                return;

            characterCardNode.IsDead = true;
            var animator = new Cthangover.CardBattle.UI.CardDeathAnimation();
            characterCardNode.AddChild(animator);
            animator.StartAnimation(characterCardNode, () =>
            {
                OnCardDead?.Invoke(characterCardNode);
                CardList.Remove(characterCardNode);
                activeTweens.Remove(characterCardNode);
                Redraw();
            });
        }
    }
}
