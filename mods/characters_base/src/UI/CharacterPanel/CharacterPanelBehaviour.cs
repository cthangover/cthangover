using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
	/// <summary>
	/// Character action panel: a header with the type legend chips and the
	/// close button, a party column of <see cref="CharacterCardBehaviour"/>
	/// and the <see cref="ActionPoolBehaviour"/> on the right, and a footer
	/// with the assignment counter and reset button.
	///
	/// The static layout (backdrop, panel frame, headers, spacers, scroll
	/// hosts) lives in <c>scenes/ui/character_panel.tscn</c>; this behaviour
	/// resolves those nodes and owns only the dynamic layer: legend badges,
	/// localized captions, character cards and their action cards (data-driven
	/// widgets built from factory data), and the drag input model.
	///
	/// Input model: drag orchestration and click-assignment both run through
	/// the node-level `_Input`, not per-card `GuiInput` — during a
	/// drag the cursor leaves the card rect and GuiInput would stop firing.
	/// A press on a card starts a potential drag; passing the 5 px threshold
	/// promotes it to a real drag. A press without motion selects the card
	/// (gold frame); clicking a socket afterwards moves or swaps it. Dropping
	/// outside a socket and outside the pool returns the card to its origin.
	/// </summary>
	public partial class CharacterPanelBehaviour : Widget
	{
		private const int DragThreshold = 5;

		// Scene nodes (character_panel.tscn)
		private Label _title;
		private HFlowContainer _legend;
		private Label _dragHint;
		private Button _closeButton;
		private Label _partyTitle;
		private Label _partyCountLabel;
		private Label _partySub;
		private HFlowContainer _charactersContainer;
		private Control _actionPoolHost;
		private Label _statusLabel;
		private Button _resetButton;

		private ActionPoolBehaviour _actionPool;
		private ActionPoolData _actionPoolData;
		private CanvasLayer _dragCanvas;

		private readonly List<CharacterCardBehaviour> _characterCards = new();
		private ActionCardBehaviour _draggedCard;
		private ActionCardBehaviour _potentialCard;
		private ActionCardBehaviour _selectedCard;
		private ActionSlotBehaviour _hoveredSlot;
		private Vector2 _localMouseOffset;
		private Vector2 _dragStartMousePos;
		private int _skipFrames;

		/// <summary>Binds scene nodes, applies theme and localized captions, and wires the dynamic layer.</summary>
		protected override void OnceConstruct()
		{
			MouseFilter = MouseFilterEnum.Stop;
			_actionPoolData = GameData.Instance.Runtime.ActionPool;

			_title = GetNode<Label>("Panel/Margin/Root/Header/Title");
			_legend = GetNode<HFlowContainer>("Panel/Margin/Root/Header/Legend");
			_dragHint = GetNode<Label>("Panel/Margin/Root/Header/DragHint");
			_closeButton = GetNode<Button>("Panel/Margin/Root/Header/CloseButton");
			_partyTitle = GetNode<Label>("Panel/Margin/Root/Body/Left/TitleRow/PartyTitle");
			_partyCountLabel = GetNode<Label>("Panel/Margin/Root/Body/Left/TitleRow/PartyCount");
			_partySub = GetNode<Label>("Panel/Margin/Root/Body/Left/TitleRow/PartySub");
			_charactersContainer = GetNode<HFlowContainer>("Panel/Margin/Root/Body/Left/Scroll/CharactersContainer");
			_actionPoolHost = GetNode<Control>("Panel/Margin/Root/Body/Right/ActionPool");
			_statusLabel = GetNode<Label>("Panel/Margin/Root/Footer/StatusLabel");
			_resetButton = GetNode<Button>("Panel/Margin/Root/Footer/ResetButton");

			ApplyTheme();
			ApplyCaptions();

			_dragCanvas = new CanvasLayer { Name = "DragCanvas", Layer = 100 };
			AddChild(_dragCanvas);

			_actionPool = CharacterPanelPrefabs.Pool.Instantiate<ActionPoolBehaviour>();
			_actionPoolHost.AddChild(_actionPool);

			_closeButton.Pressed += Hide;
			_resetButton.Pressed += OnResetClick;

			foreach (var type in ActionCharacterUi.LegendTypes)
			{
				_legend.AddChild(ActionCharacterUi.MakeBadge(
					ActionCharacterUi.Translate(ActionCharacterUi.TypeLabelKey(type)).ToUpperInvariant(),
					ActionCharacterUi.AccentColor(type)));
			}
		}

		/// <summary>Theme overrides for scene nodes that depend on UiKit tokens (styles stay code-side like the rest of the toolkit).</summary>
		private void ApplyTheme()
		{
			var panel = GetNode<Panel>("Panel");
			panel.AddThemeStyleboxOverride("panel", UiKit.Frame(
				UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));

			_title.AddThemeColorOverride("font_color", ActionCharacterUi.InkBright);
			_dragHint.AddThemeColorOverride("font_color", UiTokens.Faint);
			_dragHint.AddThemeColorOverride("font_outline_color", UiTokens.Alpha(Colors.Black, 1f));
			_dragHint.AddThemeConstantOverride("outline_size", 4);

			_partyTitle.AddThemeColorOverride("font_color", UiTokens.Muted);
			_partyCountLabel.AddThemeColorOverride("font_color", UiTokens.Body);
			_partySub.AddThemeColorOverride("font_color", UiTokens.Faint);
			_statusLabel.AddThemeColorOverride("font_color", UiTokens.Faint);

			var right = GetNode<PanelContainer>("Panel/Margin/Root/Body/Right");
			var style = UiKit.Frame(0, UiTokens.SideBg);
			style.BorderColor = UiTokens.Line;
			style.BorderWidthLeft = 1;
			style.ContentMarginLeft = 16;
			style.ContentMarginRight = 16;
			style.ContentMarginTop = 14;
			style.ContentMarginBottom = 14;
			right.AddThemeStyleboxOverride("panel", style);

			foreach (var ruleName in new[] { "Rule1", "Rule2" })
			{
				var rule = GetNode<Panel>($"Panel/Margin/Root/{ruleName}");
				rule.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line, UiTokens.Line));
			}

			CthangoverTheme.ApplyIcon(_closeButton);
			CthangoverTheme.ApplyDanger(_resetButton);

			var backdrop = GetNode<ColorRect>("Backdrop");
			UiKit.ApplyPanelBackdrop(backdrop);
		}

		/// <summary>Localized captions for all static labels of the scene.</summary>
		private void ApplyCaptions()
		{
			_title.Text = ActionCharacterUi.Translate("ui/character_panel/title").ToUpperInvariant();
			_dragHint.Text = ActionCharacterUi.Translate("ui/character_panel/drag_hint");
			_partyTitle.Text = ActionCharacterUi.Translate("ui/character_panel/party").ToUpperInvariant();
			_partySub.Text = ActionCharacterUi.Translate("ui/character_panel/party_sub");
			_closeButton.TooltipText = ActionCharacterUi.Translate("ui/character_panel/close");
			_resetButton.Text = ActionCharacterUi.Translate("ui/character_panel/reset");
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

		/// <summary>Counts down the post-rebuild input skip frames.</summary>
		public override void _Process(double delta)
		{
			if (_skipFrames > 0)
				_skipFrames--;
		}

		/// <summary>Routes mouse input to card dragging; skipped for a few frames after layout rebuilds to avoid accidental drags.</summary>
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

				var card = CharacterPanelPrefabs.CardInstance();
				if (card == null)
					continue;
				card.Name = $"Card_{characterId}";
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

			ActionAssignmentReset.Apply(characterData, _actionPoolData, CharacterFactory.Instance.Get);

			ReloadCharacters();
			GameLogger.Log("UI", "CharacterPanel.PerformReset: assignments restored to templates, awarded cards returned to pool");
		}
	}
}
