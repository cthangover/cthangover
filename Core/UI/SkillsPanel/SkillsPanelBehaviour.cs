using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Skills;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.SkillsPanel
{
	public partial class SkillsPanelBehaviour : Widget
	{
		private ScrollContainer _scrollContainer;
		private Control _gridContent;
		private Button _closeButton;

		private Control _expandedOverlay;
		private TextureRect _expandedImage;
		private Label _expandedName;
		private RichTextLabel _descriptionLabel;

		private PackedScene _skillCardPrefab;
		private Texture2D _frameTexture;
		private Texture2D _frameFullTexture;
		private Godot.Collections.Array<Color> _rareColors;

		private SkillInfo _expandedSkill;
		private readonly List<SkillCardBehaviour> _cards = new();

		private const int Columns = 6;

		public override void _Ready()
		{
			base._Ready();

			_scrollContainer = GetNodeOrNull<ScrollContainer>("ScrollContainer");
			_gridContent = GetNodeOrNull<Control>("ScrollContainer/GridContent");

			_closeButton = GetNodeOrNull<Button>("ClosePanel/CloseButton");
			if (_closeButton != null)
				_closeButton.Pressed += () => Hide();

			_expandedOverlay = GetNodeOrNull<Control>("ExpandedOverlay");
			if (_expandedOverlay != null)
			{
				_expandedOverlay.Visible = false;
				_expandedOverlay.GuiInput += OnExpandedOverlayGuiInput;
			}
			_expandedImage = GetNodeOrNull<TextureRect>("ExpandedOverlay/ExpandedImage");
			_expandedName = GetNodeOrNull<Label>("ExpandedOverlay/ExpandedName");
			_descriptionLabel = GetNodeOrNull<RichTextLabel>("ExpandedOverlay/DescriptionBg/DescriptionLabel");

			_skillCardPrefab = GD.Load<PackedScene>("res://scenes/ui/skills_panel/skill_card.tscn");
			_frameTexture = TextureUtils.LoadFromModGroup("ui/skills", "skill_frame");
			_frameFullTexture = TextureUtils.LoadFromModGroup("ui/skills", "skill_frame_full");

			_rareColors = new Godot.Collections.Array<Color>
			{
				new Color(0.5f, 0.5f, 0.5f, 1f),
				new Color(0.2f, 0.8f, 0.2f, 1f),
				new Color(0.2f, 0.5f, 1f, 1f),
				new Color(0.6f, 0.2f, 1f, 1f),
				new Color(1f, 0.55f, 0f, 1f),
			};

			GameLogger.Log("SKILLSPANEL", $"_Ready: scroll={_scrollContainer != null} grid={_gridContent != null} closeBtn={_closeButton != null} overlay={_expandedOverlay != null} prefab={_skillCardPrefab != null} frameTex={_frameTexture != null} fullTex={_frameFullTexture != null}");
		}

		protected override void ShowConstruct()
		{
			HideExpandedView();
			RebuildCards();
		}

		protected override void HideDestruct()
		{
			HideExpandedView();
			ClearCards();
		}

		private void OnExpandedOverlayGuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HideExpandedView();
				AcceptEvent();
			}
		}

		private void RebuildCards()
		{
			ClearCards();

			var skillIds = GameData.Instance.Runtime.SkillData.Skills;
			GameLogger.Log("SKILLSPANEL", $"RebuildCards: {skillIds.Count} skill IDs in data: [{string.Join(", ", skillIds)}]");

			if (_gridContent == null || _skillCardPrefab == null)
			{
				GameLogger.Log("SKILLSPANEL", $"RebuildCards: SKIP - gridContent={_gridContent != null} prefab={_skillCardPrefab != null}", LogLevel.Error);
				return;
			}

			var skills = skillIds
				.Select(id => SkillFactory.Instance.Get(id))
				.Where(s => s != null)
				.ToList();

			GameLogger.Log("SKILLSPANEL", $"RebuildCards: resolved {skills.Count} skills from factory");

			if (skills.Count == 0)
				return;

			var cardWidth = CalculateCardWidth();
			var cardHeight = cardWidth * 1.3f;
			GameLogger.Log("SKILLSPANEL", $"RebuildCards: cardSize=({cardWidth:F0}x{cardHeight:F0})");

			for (int i = 0; i < skills.Count; i++)
			{
				var skill = skills[i];
				var card = _skillCardPrefab.Instantiate<SkillCardBehaviour>();
				card.CustomMinimumSize = new Vector2(cardWidth, cardHeight);

				var frame = card.GetNodeOrNull<SkillCardFrameBehaviour>("Frame");
				if (frame != null)
				{
					var frameImg = frame.GetNodeOrNull<TextureRect>("FrameImg");
					if (frameImg != null)
						frameImg.Texture = _frameTexture;
					frame.SetColor(_rareColors[(int)skill.RareType]);
				}

				card.Construct(skill);
				card.MouseFilter = MouseFilterEnum.Stop;

				var captured = skill;
				card.GuiInput += (InputEvent e) =>
				{
					if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					{
						ShowExpandedView(captured);
						AcceptEvent();
					}
				};

				_gridContent.AddChild(card);
				PlaceCard(card, i, cardWidth, cardHeight);
				_cards.Add(card);
			}

			GameLogger.Log("SKILLSPANEL", $"RebuildCards: created {_cards.Count} cards");

			if (_cards.Count > 0)
			{
				var lastCard = _cards[_cards.Count - 1];
				var lastRow = lastCard.Position.Y + lastCard.Size.Y;
				_gridContent.CustomMinimumSize = new Vector2(0, lastRow);
			}
		}

		private float CalculateCardWidth()
		{
			var rootWidth = GetViewportRect().Size.X;
			if (rootWidth <= 0)
				rootWidth = 1920f;
			var contentWidth = rootWidth - 40f; // margins
			var gapWidth = (Columns - 1) * 10f;
			return (contentWidth - gapWidth) / Columns;
		}

		private void PlaceCard(SkillCardBehaviour card, int index, float cardWidth, float cardHeight)
		{
			var col = index % Columns;
			var row = index / Columns;
			var x = col * (cardWidth + 10f);
			var y = row * (cardHeight + 10f);
			card.Position = new Vector2(x, y);
			card.Size = new Vector2(cardWidth, cardHeight);
		}

		private void ClearCards()
		{
			foreach (var card in _cards)
				card.QueueFree();
			_cards.Clear();
		}

		private void ShowExpandedView(SkillInfo skill)
		{
			_expandedSkill = skill;

			if (_expandedOverlay != null)
				_expandedOverlay.Visible = true;

			if (_expandedImage != null)
				_expandedImage.Texture = skill.Sprite;

			if (_expandedName != null)
				_expandedName.Text = TranslationServer.Translate(skill.Name);

			if (_descriptionLabel != null)
				_descriptionLabel.Text = TranslationServer.Translate(skill.Description);

			if (_expandedOverlay != null)
			{
				var frameImg = _expandedOverlay.GetNodeOrNull<TextureRect>("ExpandedFrameImg");
				if (frameImg != null)
				{
					frameImg.Texture = _frameFullTexture;
					var colorIdx = (int)skill.RareType;
					if (colorIdx < _rareColors.Count)
						frameImg.Modulate = _rareColors[colorIdx];
				}
			}
		}

		private void HideExpandedView()
		{
			_expandedSkill = null;

			if (_expandedOverlay != null)
				_expandedOverlay.Visible = false;
		}
	}
}
