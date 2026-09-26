using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
	/// <summary>
	/// Character action panel following mockup 1: a header with the five type
	/// legend chips and the standard close button, a party column of
	/// <see cref="CharacterCardBehaviour"/> and the <see cref="ActionPoolBehaviour"/>
	/// on the right, and a footer with the assignment counter and reset button.
	/// The panel builds its whole tree in code because the card language (type
	/// accents, badges, property chips, tooltips) is data-driven and cannot live
	/// in a tscn.
	///
	/// Input model: drag orchestration and click-assignment both run through
	/// the node-level <c>_Input</c>, not per-card <c>GuiInput</c> — during a
	/// drag the cursor leaves the card rect and GuiInput would stop firing.
	/// A press on a card starts a potential drag; passing the 5 px threshold
	/// promotes it to a real drag. A press without motion selects the card
	/// (gold frame); clicking a socket afterwards moves or swaps it. Dropping
	/// outside a socket and outside the pool returns the card to its origin.
	/// </summary>
	public partial class CharacterPanelBehaviour : Widget
	{
		private const int DragThreshold = 5;

		private ActionPoolBehaviour _actionPool;
		private ActionPoolData _actionPoolData;
		private Label _statusLabel;
		private Label _partyCountLabel;
		private HFlowContainer _charactersContainer;
		private CanvasLayer _dragCanvas;

		private readonly List<CharacterCardBehaviour> _characterCards = new();
		private ActionCardBehaviour _draggedCard;
		private ActionCardBehaviour _potentialCard;
		private ActionCardBehaviour _selectedCard;
		private ActionSlotBehaviour _hoveredSlot;
		private Vector2 _localMouseOffset;
		private Vector2 _dragStartMousePos;
		private int _skipFrames;

		protected override void OnceConstruct()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Stop;
			_actionPoolData = GameData.Instance.Runtime.ActionPool;

			_dragCanvas = new CanvasLayer { Name = "DragCanvas", Layer = 100 };
			AddChild(_dragCanvas);

			var backdrop = new ColorRect();
			backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			backdrop.Color = UiTokens.Backdrop;
			backdrop.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(backdrop);

			var panel = new Panel();
			panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			panel.OffsetLeft = 40;
			panel.OffsetTop = 70;
			panel.OffsetRight = -40;
			panel.OffsetBottom = -30;
			panel.AddThemeStyleboxOverride("panel", UiKit.Frame(
				UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));
			AddChild(panel);

			var margin = new MarginContainer();
			margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			margin.AddThemeConstantOverride("margin_left", 24);
			margin.AddThemeConstantOverride("margin_right", 24);
			margin.AddThemeConstantOverride("margin_top", 14);
			margin.AddThemeConstantOverride("margin_bottom", 14);
			panel.AddChild(margin);

			var root = new VBoxContainer();
			root.AddThemeConstantOverride("separation", 10);
			margin.AddChild(root);

			root.AddChild(BuildHeader());
			root.AddChild(MakeRule());
			root.AddChild(BuildBody());
			root.AddChild(MakeRule());
			root.AddChild(BuildFooter());
		}

		protected override void ShowConstruct()
		{
			ReloadCharacters();
			_skipFrames = 2;
		}

		protected override void HideDestruct()
		{
			ClearCharacterCards();
			ClearSelection();
		}

		/// <summary>
		/// Appends an action ID to the persistent pool and refreshes the list.
		/// Public entry point for scenarios and mods that award collectible
		/// action cards.
		/// </summary>
		public void AddActionToPool(string actionId)
		{
			if (string.IsNullOrEmpty(actionId))
				return;
			_actionPoolData?.ActionIds.Add(actionId);
			RefreshPool();
		}

		public override void _Process(double delta)
		{
			if (_skipFrames > 0)
				_skipFrames--;
		}

		public override void _Input(InputEvent @event)
		{
			if (!Visible || _skipFrames > 0)
				return;

			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
					OnLeftPressed(mb.GlobalPosition);
				else
					OnLeftReleased(mb.GlobalPosition);
				return;
			}

			if (@event is InputEventMouseMotion mm)
			{
				if (_draggedCard != null)
				{
					_draggedCard.GlobalPosition = mm.GlobalPosition - _localMouseOffset;
					UpdateSlotHighlight(mm.GlobalPosition);
					AcceptEvent();
				}
				else if (_potentialCard != null
					&& (mm.GlobalPosition - _dragStartMousePos).Length() > DragThreshold)
				{
					StartDrag(_potentialCard);
				}
			}
		}

		private void OnLeftPressed(Vector2 mousePos)
		{
			if (_draggedCard != null)
				return;

			var card = FindValidCardAt(mousePos);
			if (card != null)
			{
				_potentialCard = card;
				_dragStartMousePos = mousePos;
				return;
			}

			var slot = FindSlotAt(mousePos);
			if (slot != null)
			{
				if (_selectedCard != null)
					MoveToSlot(_selectedCard, slot);
				return;
			}

			if (_selectedCard != null && !IsOverPool(mousePos))
				ClearSelection();
		}

		private void OnLeftReleased(Vector2 mousePos)
		{
			if (_draggedCard != null)
			{
				FinishDrag(mousePos);
				AcceptEvent();
				return;
			}

			if (_potentialCard == null)
				return;

			var card = _potentialCard;
			_potentialCard = null;
			HandleCardClick(card);
		}

		private void StartDrag(ActionCardBehaviour card)
		{
			if (!GodotObject.IsInstanceValid(card))
				return;

			_potentialCard = null;
			_draggedCard = card;
			var sourceSlot = card.CurrentSlot;
			ClearSelection();

			_localMouseOffset = GetGlobalMousePosition() - card.GlobalPosition;
			var globalPos = card.GlobalPosition;
			var cardSize = card.Size;

			GameLogger.Log("DRAG", $"StartDrag: action='{card.ActionData?.ID}', "
				+ $"source={(sourceSlot != null ? $"slot[{sourceSlot.SlotIndex}] char='{sourceSlot.CharacterId}'" : "pool")}");

			if (sourceSlot == null)
			{
				_actionPool?.RemoveCard(card);
				RemovePoolId(card.ActionData?.ID);
			}

			card.GetParent()?.RemoveChild(card);
			_dragCanvas.AddChild(card);
			card.SetAnchorsPreset(LayoutPreset.TopLeft, false);
			card.Size = cardSize;
			card.GlobalPosition = globalPos;
			card.ZIndex = 100;
			card.SetDragging(true);
		}

		private void FinishDrag(Vector2 mousePos)
		{
			ClearHoverHighlight();

			var card = _draggedCard;
			_draggedCard = null;

			var targetSlot = FindSlotAt(mousePos);
			if (targetSlot != null && targetSlot != card.CurrentSlot)
				MoveToSlot(card, targetSlot);
			else if (IsOverPool(mousePos))
				MoveToPool(card);
			else
				RestoreCard(card);

			if (GodotObject.IsInstanceValid(card))
				card.SetDragging(false);
			UpdateStatus();
		}

		private void HandleCardClick(ActionCardBehaviour card)
		{
			if (!GodotObject.IsInstanceValid(card))
				return;

			if (_selectedCard == card)
			{
				ClearSelection();
				return;
			}

			if (_selectedCard != null && card.CurrentSlot != null)
			{
				MoveToSlot(_selectedCard, card.CurrentSlot);
				return;
			}

			SelectCard(card);
		}

		private void SelectCard(ActionCardBehaviour card)
		{
			ClearSelection();
			_selectedCard = card;
			card.SetSelected(true);
		}

		private void ClearSelection()
		{
			if (_selectedCard != null && GodotObject.IsInstanceValid(_selectedCard))
				_selectedCard.SetSelected(false);
			_selectedCard = null;
		}

		/// <summary>
		/// Moves (or swaps) a card into the target slot. A card dragged from
		/// another slot swaps with its displaced occupant; a card taken from
		/// the pool sends the displaced card back to the pool and consumes one
		/// pool-list entry (duplicates remain untouched).
		/// </summary>
		private void MoveToSlot(ActionCardBehaviour card, ActionSlotBehaviour target)
		{
			if (card == null || target == null || !GodotObject.IsInstanceValid(card))
				return;

			var source = card.CurrentSlot;
			if (source == target)
			{
				ClearSelection();
				return;
			}

			var displaced = target.CurrentCard;
			var poolChanged = source == null;

			if (source == null)
				_actionPool?.RemoveCard(card);

			target.PlaceCard(card);

			if (displaced != null)
			{
				if (source != null)
					source.PlaceCard(displaced);
				else
					AddPoolId(displaced.ActionData?.ID);
			}

			if (poolChanged)
				RemovePoolId(card.ActionData?.ID);

			CommitCards();
			if (poolChanged)
				RefreshPool();
			ClearSelection();
			UpdateStatus();

			GameLogger.Log("DRAG", $"Move: action='{card.ActionData?.ID}' -> "
				+ $"slot[{target.SlotIndex}] char='{target.CharacterId}'{(displaced != null ? $", displaced='{displaced.ActionData?.ID}'" : string.Empty)}");
		}

		private void MoveToPool(ActionCardBehaviour card)
		{
			var source = card.CurrentSlot;
			source?.RemoveCard();
			card.SetCompact(true);
			_actionPool?.AcceptCard(card);
			AddPoolId(card.ActionData?.ID);

			if (source != null)
				CommitCards();
			RefreshPool();
			UpdateStatus();
		}

		private void RestoreCard(ActionCardBehaviour card)
		{
			if (card.CurrentSlot != null)
			{
				card.ReturnToOrigin();
				return;
			}

			card.QueueFree();
			AddPoolId(card.ActionData?.ID);
			RefreshPool();
			UpdateStatus();
		}

		private void UpdateSlotHighlight(Vector2 mousePos)
		{
			var newHovered = FindSlotAt(mousePos);
			if (newHovered == _hoveredSlot)
				return;

			ClearHoverHighlight();
			_hoveredSlot = newHovered;
			_hoveredSlot?.SetHighlight(true);
		}

		private void ClearHoverHighlight()
		{
			_hoveredSlot?.SetHighlight(false);
			_hoveredSlot = null;
		}

		private void ReloadCharacters()
		{
			ClearCharacterCards();

			var characterData = GameData.Instance.Runtime.CharacterData;
			if (characterData?.BattleSet == null || characterData.Characters == null)
			{
				RefreshPool();
				UpdateStatus();
				return;
			}

			foreach (var characterId in characterData.BattleSet)
			{
				if (!characterData.Characters.TryGetValue(characterId, out var info))
					continue;

				var card = new CharacterCardBehaviour { Name = $"Card_{characterId}" };
				_charactersContainer.AddChild(card);
				card.SetCharacter(characterId, info);
				_characterCards.Add(card);
			}

			RefreshPool();
			UpdateStatus();
		}

		private void ClearCharacterCards()
		{
			foreach (var card in _characterCards)
				card.QueueFree();
			_characterCards.Clear();
		}

		private void RefreshPool()
		{
			_actionPool?.Refresh(_actionPoolData?.ActionIds);
		}

		private void CommitCards()
		{
			foreach (var card in _characterCards)
				card.CommitSlots();
		}

		private void AddPoolId(string id)
		{
			if (!string.IsNullOrEmpty(id))
				_actionPoolData?.ActionIds.Add(id);
		}

		private void RemovePoolId(string id)
		{
			if (!string.IsNullOrEmpty(id))
				_actionPoolData?.ActionIds.Remove(id);
		}

		private void UpdateStatus()
		{
			if (_statusLabel == null)
				return;

			var assigned = _characterCards.Sum(card => card.Slots.Count(slot => slot != null && !slot.IsEmpty));
			var total = _characterCards.Count * 3;
			var pool = _actionPoolData?.ActionIds?.Count ?? 0;
			_statusLabel.Text = string.Format(
				ActionCharacterUi.Translate("ui/character_panel/assigned"), assigned, total, pool)
				+ " · " + ActionCharacterUi.Translate("ui/character_panel/hint_select");

			if (_partyCountLabel != null)
				_partyCountLabel.Text = _characterCards.Count.ToString();
		}

		private ActionCardBehaviour FindValidCardAt(Vector2 globalPos)
		{
			foreach (var characterCard in _characterCards)
			{
				foreach (var slot in characterCard.Slots)
				{
					var card = slot?.CurrentCard;
					if (card == null || !GodotObject.IsInstanceValid(card))
						continue;
					if (card.GetGlobalRect().HasPoint(globalPos))
						return card;
				}
			}

			if (_actionPool != null)
			{
				foreach (var poolCard in _actionPool.VisibleCards)
				{
					if (GodotObject.IsInstanceValid(poolCard) && poolCard.GetGlobalRect().HasPoint(globalPos))
						return poolCard;
				}
			}

			return null;
		}

		private ActionSlotBehaviour FindSlotAt(Vector2 globalPos)
		{
			foreach (var characterCard in _characterCards)
			{
				foreach (var slot in characterCard.Slots)
				{
					if (slot != null && slot.GetGlobalRect().HasPoint(globalPos))
						return slot;
				}
			}
			return null;
		}

		private bool IsOverPool(Vector2 globalPos)
		{
			return _actionPool != null && _actionPool.GetGlobalRect().HasPoint(globalPos);
		}

		private Control BuildHeader()
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 18);
			row.MouseFilter = MouseFilterEnum.Ignore;

			row.AddChild(ActionCharacterUi.MakeLabel(
				ActionCharacterUi.Translate("ui/character_panel/title").ToUpperInvariant(), 22, ActionCharacterUi.InkBright));

			var legend = new HFlowContainer();
			legend.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			legend.AddThemeConstantOverride("h_separation", 5);
			legend.AddThemeConstantOverride("v_separation", 4);
			legend.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var type in ActionCharacterUi.LegendTypes)
			{
				legend.AddChild(ActionCharacterUi.MakeBadge(
					ActionCharacterUi.Translate(ActionCharacterUi.TypeLabelKey(type)).ToUpperInvariant(),
					ActionCharacterUi.AccentColor(type)));
			}
			row.AddChild(legend);

			row.AddChild(ActionCharacterUi.MakeLabel(
				ActionCharacterUi.Translate("ui/character_panel/drag_hint"), 11, ActionCharacterUi.Faint));

			var closeButton = new Button
			{
				Text = "✕",
				TooltipText = ActionCharacterUi.Translate("ui/character_panel/close"),
				FocusMode = FocusModeEnum.None,
				CustomMinimumSize = new Vector2(38, 38),
			};
			CthangoverTheme.ApplyIcon(closeButton);
			closeButton.Pressed += Hide;
			row.AddChild(closeButton);

			return row;
		}

		private Control BuildBody()
		{
			var body = new HBoxContainer();
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 14);
			body.MouseFilter = MouseFilterEnum.Pass;

			var left = new VBoxContainer();
			left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			left.AddThemeConstantOverride("separation", 8);
			left.MouseFilter = MouseFilterEnum.Pass;
			body.AddChild(left);

			var titleRow = new HBoxContainer();
			titleRow.AddThemeConstantOverride("separation", 8);
			titleRow.MouseFilter = MouseFilterEnum.Ignore;
			titleRow.AddChild(ActionCharacterUi.MakeLabel(
				ActionCharacterUi.Translate("ui/character_panel/party").ToUpperInvariant(), 12, ActionCharacterUi.Muted));
			_partyCountLabel = ActionCharacterUi.MakeLabel("0", 11, ActionCharacterUi.Body);
			titleRow.AddChild(_partyCountLabel);

			var titleSpacer = new Control();
			titleSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			titleSpacer.MouseFilter = MouseFilterEnum.Ignore;
			titleRow.AddChild(titleSpacer);
			titleRow.AddChild(ActionCharacterUi.MakeLabel(
				ActionCharacterUi.Translate("ui/character_panel/party_sub"), 11, ActionCharacterUi.Faint));
			left.AddChild(titleRow);

			var scroll = new ScrollContainer();
			scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			left.AddChild(scroll);

			_charactersContainer = new HFlowContainer();
			_charactersContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_charactersContainer.AddThemeConstantOverride("h_separation", 14);
			_charactersContainer.AddThemeConstantOverride("v_separation", 14);
			_charactersContainer.MouseFilter = MouseFilterEnum.Pass;
			scroll.AddChild(_charactersContainer);

			var right = new PanelContainer();
			right.CustomMinimumSize = new Vector2(420, 0);
			right.AddThemeStyleboxOverride("panel", MakePoolPanelStyle());
			body.AddChild(right);

			_actionPool = new ActionPoolBehaviour();
			right.AddChild(_actionPool);

			return body;
		}

		private Control BuildFooter()
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 10);
			row.MouseFilter = MouseFilterEnum.Ignore;

			_statusLabel = ActionCharacterUi.MakeLabel(string.Empty, 12, ActionCharacterUi.Faint);
			row.AddChild(_statusLabel);

			var spacer = new Control();
			spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			spacer.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(spacer);

			var resetButton = new Button();
			resetButton.Text = ActionCharacterUi.Translate("ui/character_panel/reset");
			resetButton.FocusMode = FocusModeEnum.None;
			CthangoverTheme.ApplyDanger(resetButton);
			resetButton.Pressed += OnResetClick;
			row.AddChild(resetButton);

			return row;
		}

		private static StyleBoxFlat MakePoolPanelStyle()
		{
			var style = UiKit.Frame(0, UiTokens.SideBg);
			style.BorderColor = UiTokens.Line;
			style.BorderWidthLeft = 1;
			style.ContentMarginLeft = 16;
			style.ContentMarginRight = 16;
			style.ContentMarginTop = 14;
			style.ContentMarginBottom = 14;
			return style;
		}

		private static Control MakeRule()
		{
			var rule = new Panel();
			rule.CustomMinimumSize = new Vector2(0, 1);
			rule.AddThemeStyleboxOverride("panel", UiKit.Frame(
				0, UiTokens.Line, UiTokens.Line));
			rule.MouseFilter = MouseFilterEnum.Ignore;
			return rule;
		}

		private void OnResetClick()
		{
			var dialog = new ConfirmationDialog();
			dialog.Title = ActionCharacterUi.Translate("ui/character_panel/reset_title");
			dialog.DialogText = ActionCharacterUi.Translate("ui/character_panel/reset_text");
			dialog.OkButtonText = ActionCharacterUi.Translate("ui/character_panel/reset_ok");
			dialog.CancelButtonText = ActionCharacterUi.Translate("ui/character_panel/reset_cancel");
			dialog.Exclusive = true;
			AddChild(dialog);

			dialog.Confirmed += () =>
			{
				PerformReset();
				dialog.QueueFree();
			};
			dialog.Canceled += () => dialog.QueueFree();
			dialog.PopupCentered();
		}

		private void PerformReset()
		{
			var characterData = GameData.Instance.Runtime.CharacterData;
			if (characterData?.BattleSet == null || characterData.Characters == null)
				return;

			var extraPoolIds = new HashSet<string>(_actionPoolData?.ActionIds ?? new List<string>());
			var assignedAfterReset = new HashSet<string>();

			foreach (var characterId in characterData.BattleSet)
			{
				if (!characterData.Characters.TryGetValue(characterId, out var info))
					continue;

				var template = CharacterFactory.Instance.Get(characterId);
				var defaultActionIds = template?.Actions?.Select(action => action.ID).ToList()
					?? new List<string>();

				var slots = new List<string>();
				for (var i = 0; i < 3; i++)
				{
					if (i < defaultActionIds.Count)
					{
						slots.Add(defaultActionIds[i]);
						assignedAfterReset.Add(defaultActionIds[i]);
					}
					else
					{
						slots.Add(null);
					}
				}

				for (var i = 3; i < defaultActionIds.Count; i++)
					extraPoolIds.Add(defaultActionIds[i]);

				info.ActionSlots = slots;
			}

			extraPoolIds.ExceptWith(assignedAfterReset);
			_actionPoolData.ActionIds = extraPoolIds.ToList();

			ReloadCharacters();
			GameLogger.Log("UI", "CharacterPanel.PerformReset: assignments restored to templates");
		}
	}
}
