using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{
    
    public class StatusEffectQueue
    {
        private readonly List<IStatusEffect> activeStatusEffectData;
        private readonly Character owner;
        
        public List<IStatusEffect> ActiveStatusEffects => activeStatusEffectData.ToList();
        
        public StatusEffectQueue(Character owner)
        {
            this.activeStatusEffectData = new List<IStatusEffect>();
            this.owner = owner;
        }
        
        public bool HasStun()
        {
            foreach (var statusEffect in activeStatusEffectData)
                if (statusEffect.EffectType == StatusEffectType.Stun)
                    return true;
            return false;
        }
        
        public bool Add(string statusEffectId, int duration = -1)
        {
            var statusEffectInfo = Factories.Impls.StatusEffectInfoFactory.Instance.Get(statusEffectId);
            
            if (statusEffectInfo == null)
            {
                GameLogger.Log("STATUS", $"statusEffect with ID '{statusEffectId}' not found!", LogLevel.Error);
                return false;
            }
            
            var statusEffect = new StatusEffectItem(statusEffectInfo);
            
            if (duration > 0)
            {
                statusEffect.Duration = duration;
                statusEffect.RemainingTurns = duration;
            }
            
            statusEffect.Actions?.OnApply(owner);
            activeStatusEffectData.Add(statusEffect);
            
            return true;
        }
        
        public bool Remove(string statusEffectId)
        {
            var statusEffect = activeStatusEffectData.FirstOrDefault(b => b.ID == statusEffectId);
            if (statusEffect == null)
                return false;
                
            statusEffect.Actions?.OnRemove(owner);
            activeStatusEffectData.Remove(statusEffect);
            
            return true;
        }
        
        public void OnTurnStart()
        {
            for (int i = activeStatusEffectData.Count - 1; i >= 0; i--)
            {
                var statusEffect = activeStatusEffectData[i];
                statusEffect.Actions?.OnTurnStart(owner);
                if (statusEffect.RemainingTurns > 0 && statusEffect.Duration > 0)
                {
                    statusEffect.RemainingTurns--;
                    if (statusEffect.RemainingTurns <= 0)
                    {
                        statusEffect.Actions?.OnFinalAction(owner);
                        activeStatusEffectData.RemoveAt(i);
                    }
                }
            }
        }
        
        public void OnTurnEnd()
        {
            foreach (var statusEffect in ActiveStatusEffects)
            {
                statusEffect.Actions?.OnTurnEnd(owner);
                statusEffect.RemainingTurns--;
                if (statusEffect.RemainingTurns <= 0)
                {
                    statusEffect.Actions?.OnFinalAction(owner);
                    Remove(statusEffect.ID);
                }
            }
        }
        
        public void OnDealDamage(Character target, ref int damage)
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnDealDamage(owner, target, ref damage);
            }
        }
        
        public void OnTakeDamage(Character source, ref int damage)
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnTakeDamage(owner, source, ref damage);
            }
        }
        
        public void ClearAll()
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnRemove(owner);
            }
            activeStatusEffectData.Clear();
        }
        
        public StatusEffectQueue Copy(Character newOwner)
        {
            var newSystem = new StatusEffectQueue(newOwner);
            foreach (var statusEffect in activeStatusEffectData)
            {
                var copiedstatusEffect = statusEffect.Copy();
                copiedstatusEffect.Actions?.OnApply(newOwner);
                newSystem.activeStatusEffectData.Add(copiedstatusEffect);
            }
            return newSystem;
        }
        
    }
}
