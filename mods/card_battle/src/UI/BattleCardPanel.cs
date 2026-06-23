using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    public partial class BattleCardPanel : ModWidget
    {
        private const int MAX_CARDS_ROW = 10;
        private const float BASE_CARD_HEIGHT = 280f;

        [Export] private Control content;
        [Export] public string AlignType { get; set; } = "Left";
        [Export] private float cellOffset = 10f;

        public List<CharacterCardNode> CardList { get; } = new();
        private Dictionary<CharacterCardNode, Tween> activeTweens = new();

        public bool UseRows { get; set; }

        public event System.Action<CharacterCardNode> OnCardDead;

        protected override void Construct() { }

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
