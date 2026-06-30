using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    /// <summary>
    /// Manages the collection of <see cref="IStatusEffect"/> instances attached to a single <see cref="Character"/>.
    /// Implements <see cref="IStatusActions"/> so that individual effects can call back into the queue
    /// (e.g. to skip the turn). During <see cref="OnTurnStart"/>, each effect's callback is invoked and
    /// its turn counter is decremented; expired effects are removed. <see cref="SkipTurn"/> is checked by
    /// <see cref="CardBattleCore.RunEnemyTurn"/> to skip stunned enemies.
    /// Supports deep copying for character duplication via <see cref="Copy"/>.
    /// </summary>
    public class StatusEffectQueue : IStatusActions
    {
        private readonly Character _character;
        private readonly List<IStatusEffect> _effects = new();

        /// <summary>
        /// Set to <c>true</c> when an effect calls <see cref="IStatusActions.SkipTurn"/> during <see cref="OnTurnStart"/>.
        /// Reset to <c>false</c> at the start of each turn before effects are processed.
        /// </summary>
        public bool SkipTurn { get; private set; }

        /// <summary>
        /// Creates a new queue bound to the given <paramref name="character"/>.
        /// </summary>
        public StatusEffectQueue(Character character)
        {
            _character = character;
        }

        /// <summary>
        /// Creates a deep copy of this queue bound to <paramref name="newCharacter"/>.
        /// <see cref="StatusEffectItem"/> effects are field-copied; other effect types are shared by reference.
        /// </summary>
        public StatusEffectQueue Copy(Character newCharacter)
        {
            var clone = new StatusEffectQueue(newCharacter);
            foreach (var effect in _effects)
            {
                if (effect is StatusEffectItem item)
                    clone._effects.Add(item.Copy() as IStatusEffect);
                else
                    clone._effects.Add(effect);
            }
            return clone;
        }

        /// <summary>
        /// Adds a new status effect to the end of the processing list. Effects are processed
        /// in reverse order during turn callbacks (last-added first).
        /// </summary>
        public void AddEffect(IStatusEffect effect)
        {
            _effects.Add(effect);
        }

        /// <summary>
        /// Processes all effects at the start of the character's turn: resets <see cref="SkipTurn"/>,
        /// calls <see cref="IStatusEffect.OnTurnStart"/> for each effect in reverse order,
        /// decrements the turn counter, and removes expired effects.
        /// Called by <see cref="CardBattleCore.StartPlayerTurn"/> and <see cref="CardBattleCore.RunEnemyTurn"/>.
        /// </summary>
        public void OnTurnStart()
        {
            SkipTurn = false;
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.OnTurnStart(_character, this);
                effect.Turns--;
                if (effect.IsExpired)
                    _effects.RemoveAt(i);
            }
        }

        /// <summary>
        /// Processes all effects at the end of the character's turn: calls
        /// <see cref="IStatusEffect.OnTurnEnd"/> in reverse order and removes expired effects.
        /// </summary>
        public void OnTurnEnd()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.OnTurnEnd(_character, this);
                if (effect.IsExpired)
                    _effects.RemoveAt(i);
            }
        }

        void IStatusActions.SkipTurn()
        {
            SkipTurn = true;
        }

        void IStatusActions.RemoveStatus(IStatusEffect status)
        {
            _effects.Remove(status);
        }
    }
}
