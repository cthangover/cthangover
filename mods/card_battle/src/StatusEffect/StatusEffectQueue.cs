using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    public class StatusEffectQueue : IStatusActions
    {
        private readonly Character _character;
        private readonly List<IStatusEffect> _effects = new();

        public bool SkipTurn { get; private set; }

        public StatusEffectQueue(Character character)
        {
            _character = character;
        }

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

        public void AddEffect(IStatusEffect effect)
        {
            _effects.Add(effect);
        }

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
