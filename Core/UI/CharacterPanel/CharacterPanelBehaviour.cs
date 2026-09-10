using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
	public partial class CharacterPanelBehaviour : Widget
	{
		private VBoxContainer _charactersContainer;
		private ActionPoolBehaviour _actionPool;
		private ActionPoolData _actionPoolData;
		private Button _resetButton;
		private Button _closeButton;

		private readonly List<CharacterCardBehaviour> _characterCards = new();
		private ActionCardBehaviour _draggedCard;
		private ActionCardBehaviour _potentialDragCard;
		private ActionSlotBehaviour _hoveredSlot;
		private Vector2 _localMouseOffset;
		private Vector2 _dragStartMousePos;
		private int _skipFrames;

		private PackedScene _characterCardScene;
		private PackedScene _actionCardScene;
		private CanvasLayer _dragCanvas;

		private const int DragThreshold = 5;

		public override void _Ready()
		{
			base._Ready();
			_charactersContainer = GetNodeOrNull<VBoxContainer>("LeftPanel/LeftVBox/ScrollContainer/CharactersContainer");
			_actionPool = GetNodeOrNull<ActionPoolBehaviour>("RightPanel/ActionPoolScroll/ActionPool");
			_actionPoolData = GameData.Instance.Runtime.ActionPool;

			_characterCardScene = GD.Load<PackedScene>("res://scenes/ui/character_panel/character_card.tscn");
			_actionCardScene = GD.Load<PackedScene>("res://scenes/ui/character_panel/action_card.tscn");

			_dragCanvas = new CanvasLayer { Name = "DragCanvas", Layer = 100 };
			AddChild(_dragCanvas);

			_closeButton = GetNodeOrNull<Button>("CloseButton");
			if (_closeButton != null)
			{
				_closeButton.Pressed += () => Hide();
				_closeButton.Text = TranslationServer.Translate("ui/character_panel/close");
			}

			_resetButton = GetNodeOrNull<Button>("ResetButton");
			if (_resetButton != null)
			{
				_resetButton.Pressed += OnResetClick;
				_resetButton.Text = TranslationServer.Translate("ui/character_panel/reset");
			}
		}

		public override void _Process(double delta)
		{
			if (_skipFrames > 0)
				_skipFrames--;
		}

		private int _dragFrameCounter;

		public override void _Input(InputEvent @event)
		{
			if (!Visible || _skipFrames > 0)
				return;

			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					var card = FindValidCardAt(mb.GlobalPosition);
					if (card != null)
					{
						_potentialDragCard = card;
						_dragStartMousePos = mb.GlobalPosition;
					}
				}
				else if (_draggedCard != null)
				{
					FinishDrag(mb.GlobalPosition);
					AcceptEvent();
				}
				else
				{
					_potentialDragCard = null;
				}
			}

			if (@event is InputEventMouseMotion mm)
			{
				if (_draggedCard != null)
				{
					var newPos = mm.GlobalPosition - _localMouseOffset;
					_draggedCard.GlobalPosition = newPos;
					_dragFrameCounter++;
					if (_dragFrameCounter % 10 == 0)
					{
						GameLogger.Log("DRAG", $"DragMotion: frame={_dragFrameCounter}, "
							+ $"mouse=({mm.GlobalPosition.X:F0},{mm.GlobalPosition.Y:F0}), "
							+ $"cardPos=({newPos.X:F0},{newPos.Y:F0}), "
							+ $"offset=({_localMouseOffset.X:F0},{_localMouseOffset.Y:F0})");
					}
					UpdateSlotHighlight(mm.GlobalPosition);
					AcceptEvent();
				}
				else if (_potentialDragCard != null)
				{
					if ((mm.GlobalPosition - _dragStartMousePos).Length() > DragThreshold)
					{
						_dragFrameCounter = 0;
						StartDrag(_potentialDragCard);
						_potentialDragCard = null;
					}
				}
			}
		}

		private void StartDrag(ActionCardBehaviour card)
		{
			_draggedCard = card;

			var mouseGlobal = GetGlobalMousePosition();
			var globalPos = card.GlobalPosition;
			var cardSize = card.Size;
			_localMouseOffset = mouseGlobal - globalPos;

			var source = card.CurrentSlot != null
				? $"slot[{card.CurrentSlot.SlotIndex}] char='{card.CurrentSlot.CharacterId}'"
				: "pool";
			GameLogger.Log("DRAG", $"StartDrag: action='{card.ActionData?.ID}', source={source}, "
				+ $"mouse=({mouseGlobal.X:F0},{mouseGlobal.Y:F0}), cardPos=({globalPos.X:F0},{globalPos.Y:F0}), "
				+ $"offset=({_localMouseOffset.X:F0},{_localMouseOffset.Y:F0}), cardSize=({cardSize.X:F0},{cardSize.Y:F0})");

			if (card.CurrentSlot == null)
			{
				_actionPool?.RemoveCard(card);
				_actionPoolData?.ActionIds.Remove(card.ActionData?.ID);
			}

			card.GetParent()?.RemoveChild(card);
			_dragCanvas.AddChild(card);
			card.SetAnchorsPreset(Control.LayoutPreset.TopLeft, false);
			card.Size = cardSize;
			card.GlobalPosition = globalPos;
			GameLogger.Log("DRAG", $"StartDrag: after reparent, card.GlobalPosition=({card.GlobalPosition.X:F0},{card.GlobalPosition.Y:F0}), card.Size=({card.Size.X:F0},{card.Size.Y:F0})");
			card.ZIndex = 100;
		}

		private void FinishDrag(Vector2 mousePos)
		{
			ClearHoverHighlight();

			var card = _draggedCard;
			_draggedCard = null;
			_hoveredSlot = null;

			var targetSlot = FindSlotAt(mousePos);

			if (targetSlot != null && targetSlot != card.CurrentSlot)
			{
				GameLogger.Log("DRAG", $"FinishDrag: action='{card.ActionData?.ID}' -> "
					+ $"slot[{targetSlot.SlotIndex}] char='{targetSlot.CharacterId}', "
					+ $"mouse=({mousePos.X:F0},{mousePos.Y:F0})");
				MoveCardToSlot(card, targetSlot);
			}
			else if (IsOverPool(mousePos))
			{
				GameLogger.Log("DRAG", $"FinishDrag: action='{card.ActionData?.ID}' -> pool, "
					+ $"mouse=({mousePos.X:F0},{mousePos.Y:F0})");
				var sourceSlot = card.CurrentSlot;
				sourceSlot?.RemoveCard();
				_actionPool.AcceptCard(card);
				if (sourceSlot != null)
				{
					var sourceCard = _characterCards.FirstOrDefault(c => c.CharacterId == sourceSlot.CharacterId);
					sourceCard?.CommitSlots();
				}
				if (card.ActionData != null)
				{
					_actionPoolData?.ActionIds.Add(card.ActionData.ID);
					RefreshPool();
				}
			}
			else
			{
				GameLogger.Log("DRAG", $"FinishDrag: action='{card.ActionData?.ID}' -> returnToOrigin, "
					+ $"mouse=({mousePos.X:F0},{mousePos.Y:F0})");
				card.ReturnToOrigin();
			}
		}

		private void UpdateSlotHighlight(Vector2 mousePos)
		{
			var newHovered = FindSlotAt(mousePos);
			if (newHovered != _hoveredSlot)
			{
				ClearHoverHighlight();
				_hoveredSlot = newHovered;
				HighlightSlot(_hoveredSlot);
			}
		}

		protected override void ShowConstruct()
		{
			ReloadCharacters();
			_skipFrames = 2;
		}

		protected override void HideDestruct()
		{
			ClearCharacterCards();
		}

		private void ReloadCharacters()
		{
			ClearCharacterCards();

			var characterData = GameData.Instance.Runtime.CharacterData;
			if (characterData?.BattleSet == null || characterData.Characters == null)
				return;

			foreach (var characterId in characterData.BattleSet)
			{
				if (!characterData.Characters.TryGetValue(characterId, out var info))
					continue;

				var card = _characterCardScene.Instantiate<CharacterCardBehaviour>();
				card.Name = $"Card_{characterId}";
				_charactersContainer.AddChild(card);
				card.SetCharacter(characterId, info, _actionCardScene);
				_characterCards.Add(card);
			}

			_actionPool?.SetActionCardScene(_actionCardScene);
			RefreshPool();
		}

		private void ClearCharacterCards()
		{
			foreach (var card in _characterCards)
				card.QueueFree();
			_characterCards.Clear();
		}

		private void MoveCardToSlot(ActionCardBehaviour card, ActionSlotBehaviour targetSlot)
		{
			var sourceSlot = card.CurrentSlot;
			var sourceCard = sourceSlot != null
				? _characterCards.FirstOrDefault(c => c.CharacterId == sourceSlot.CharacterId)
				: null;
			var targetCard = _characterCards.FirstOrDefault(c => c.CharacterId == targetSlot.CharacterId);

			var movedFromPool = sourceSlot == null;

			if (targetSlot.IsEmpty)
			{
				targetSlot.PlaceCard(card);
			}
			else
			{
				var displacedCard = targetSlot.CurrentCard;
				targetSlot.PlaceCard(card);

				if (sourceSlot != null)
				{
					sourceSlot.PlaceCard(displacedCard);
				}
				else
				{
					_actionPool.AcceptCard(displacedCard);
					if (displacedCard.ActionData != null)
						_actionPoolData?.ActionIds.Add(displacedCard.ActionData.ID);
				}
			}

			sourceCard?.CommitSlots();
			if (targetCard != null && targetCard != sourceCard)
				targetCard.CommitSlots();

			RefreshPool();
		}

		private void RefreshPool()
		{
			_actionPool?.Refresh(_actionPoolData?.ActionIds);
		}

		public void AddActionToPool(string actionId)
		{
			if (string.IsNullOrEmpty(actionId))
				return;
			_actionPoolData?.ActionIds.Add(actionId);
			RefreshPool();
		}

		private void OnResetClick()
		{
			var dialog = new ConfirmationDialog();
			dialog.Title = TranslationServer.Translate("ui/character_panel/reset_title");
			dialog.DialogText = TranslationServer.Translate("ui/character_panel/reset_text");
			dialog.OkButtonText = TranslationServer.Translate("ui/character_panel/reset_ok");
			dialog.CancelButtonText = TranslationServer.Translate("ui/character_panel/reset_cancel");
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
				var defaultActionIds = template?.Actions?.Select(a => a.ID).ToList()
					?? new List<string>();

				var slots = new List<string>();
				for (int i = 0; i < 3; i++)
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

				for (int i = 3; i < defaultActionIds.Count; i++)
				{
					extraPoolIds.Add(defaultActionIds[i]);
				}

				info.ActionSlots = slots;
			}

			extraPoolIds.ExceptWith(assignedAfterReset);
			_actionPoolData.ActionIds = extraPoolIds.ToList();

			ReloadCharacters();
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
				var container = _actionPool.GetNodeOrNull<VBoxContainer>("Container");
				if (container != null)
				{
					foreach (var child in container.GetChildren())
					{
						if (child is ActionCardBehaviour poolCard
							&& GodotObject.IsInstanceValid(poolCard)
							&& poolCard.GetGlobalRect().HasPoint(globalPos))
							return poolCard;
					}
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
					if (slot == null)
						continue;
					if (slot.GetGlobalRect().HasPoint(globalPos))
						return slot;
				}
			}
			return null;
		}

		private bool IsOverPool(Vector2 globalPos)
		{
			if (_actionPool == null)
				return false;
			return _actionPool.GetGlobalRect().HasPoint(globalPos);
		}

		private void HighlightSlot(ActionSlotBehaviour slot)
		{
			if (slot == null)
				return;
			var bg = slot.GetNodeOrNull<ColorRect>("Bg");
			if (bg != null)
				bg.Color = new Color(0.2f, 0.4f, 0.2f, 0.6f);
		}

		private void ClearHoverHighlight()
		{
			if (_hoveredSlot != null)
			{
				var bg = _hoveredSlot.GetNodeOrNull<ColorRect>("Bg");
				if (bg != null)
					bg.Color = new Color(0.1f, 0.1f, 0.15f, 0.5f);
			}
		}
	}
}
