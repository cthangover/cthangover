using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
	/// <summary>
	/// Displays action cards from the persistent <see cref="Settings.ActionPoolData"/>.
	/// Shows exactly what is in the persisted pool — no filtering by assigned slots.
	/// </summary>
	public partial class ActionPoolBehaviour : Control
	{
		private VBoxContainer _container;
		private readonly List<ActionCardBehaviour> _poolCards = new();
		private PackedScene _actionCardScene;

		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Stop;
			_container = GetNodeOrNull<VBoxContainer>("Container");
		}

		public void SetActionCardScene(PackedScene scene)
		{
			_actionCardScene = scene;
		}

		/// <summary>
		/// Repopulates the pool from the persisted <paramref name="poolActionIds"/> list.
		/// No filtering — every ID in the list gets a card, including duplicates.
		/// </summary>
		public void Refresh(List<string> poolActionIds)
		{
			if (_actionCardScene == null)
				return;

			ClearPool();

			if (poolActionIds == null)
				return;

			foreach (var actionId in poolActionIds)
			{
				var action = ActionCharacterFactory.Instance.Get(actionId);
				if (action != null)
					AddPoolCard(action);
			}
		}

		/// <summary>
		/// Accepts a card into the pool, removing it from its current slot.
		/// The VBoxContainer manages sizing — size_flags control width stretch.
		/// </summary>
		public void AcceptCard(ActionCardBehaviour card)
		{
			if (card == null)
				return;

			card.CurrentSlot?.RemoveCard();
			card.CurrentSlot = null;

			if (card.GetParent() != _container)
			{
				card.GetParent()?.RemoveChild(card);
				_container.AddChild(card);
			}

			card.SetAnchorsPreset(Control.LayoutPreset.TopLeft, false);
			card.SizeFlagsHorizontal = Control.SizeFlags.Expand;
			_poolCards.Add(card);
		}

		public void RemoveCard(ActionCardBehaviour card)
		{
			if (card == null)
				return;
			_poolCards.Remove(card);
			card.GetParent()?.RemoveChild(card);
		}

		private void AddPoolCard(ActionCharacter action)
		{
			var card = _actionCardScene.Instantiate<ActionCardBehaviour>();
			card.SetAction(action);
			card.SetAnchorsPreset(Control.LayoutPreset.TopLeft, false);
			card.SizeFlagsHorizontal = Control.SizeFlags.Expand;
			_container.AddChild(card);
			_poolCards.Add(card);
		}

		private void ClearPool()
		{
			foreach (var card in _poolCards)
				card.QueueFree();
			_poolCards.Clear();
		}
	}
}
