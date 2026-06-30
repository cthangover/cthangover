using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{
    
    /// <summary>
    /// Per-character status-effect pipeline. Manages a list of active
    /// effects, advancing their timers on turn boundaries and removing
    /// expired ones after firing OnFinalAction. Add resolves the effect
    /// info via the factory and calls OnApply immediately. OnTurnStart
    /// iterates backwards to safely remove expired effects during
    /// traversal. OnDealDamage / OnTakeDamage pipe through every active
    /// effect via ref parameters, enabling multiplicative stacking.
    /// ActiveStatusEffects returns a defensive copy so external code
    /// cannot mutate the internal list. Copy creates a deep clone for
    /// character duplication, re-firing OnApply on the new owner.
    /// </summary>
    public class StatusEffectQueue
    {
        private readonly List<IStatusEffect> activeStatusEffectData;
        private readonly Character owner;
        
        /// <summary>Defensive copy of the internal effect list. Returns
        /// a new <c>List</c> each access so external code cannot mutate
        /// the queue's backing store. Safe for enumeration.</summary>
        public List<IStatusEffect> ActiveStatusEffects => activeStatusEffectData.ToList();
        
        /// <summary>Initialises an empty queue bound to <paramref name="owner"/>.
        /// The owner reference is used by all action-hook calls to pass
        /// the affected character.</summary>
        /// <param name="owner">The character who carries these
        /// effects.</param>
        public StatusEffectQueue(Character owner)
        {
            this.activeStatusEffectData = new List<IStatusEffect>();
            this.owner = owner;
        }
        
        /// <summary>Checks whether any active effect has
        /// <see cref="StatusEffectType.Stun"/>. Called by the battle
        /// engine to determine if the owner should skip their turn.</summary>
        /// <returns><see langword="true"/> if at least one Stun-type effect
        /// is active.</returns>
        public bool HasStun()
        {
            foreach (var statusEffect in activeStatusEffectData)
                if (statusEffect.EffectType == StatusEffectType.Stun)
                    return true;
            return false;
        }
        
        /// <summary>Looks up <paramref name="statusEffectId"/> in the factory,
        /// constructs a <see cref="StatusEffectItem"/>, optionally
        /// overrides its duration, fires <c>OnApply</c>, and appends it
        /// to the active list.</summary>
        /// <param name="statusEffectId">Factory key matching a
        /// <see cref="StatusEffectInfo"/> entry.</param>
        /// <param name="duration">If positive, overwrites the default
        /// duration from the effect definition.</param>
        /// <returns><see langword="false"/> if the factory lookup fails
        /// (key not found); otherwise <see langword="true"/>.</returns>
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
        
        /// <summary>Removes the first effect matching
        /// <paramref name="statusEffectId"/> from the active list.
        /// Fires <c>OnRemove</c> so the effect can clean up before
        /// disappearing (e.g. revert stat modifications).</summary>
        /// <param name="statusEffectId">Factory key of the effect to
        /// remove.</param>
        /// <returns><see langword="false"/> if no matching effect was
        /// found; otherwise <see langword="true"/>.</returns>
        public bool Remove(string statusEffectId)
        {
            var statusEffect = activeStatusEffectData.FirstOrDefault(b => b.ID == statusEffectId);
            if (statusEffect == null)
                return false;
                
            statusEffect.Actions?.OnRemove(owner);
            activeStatusEffectData.Remove(statusEffect);
            
            return true;
        }
        
        /// <summary>Advances all active effects at the start of the owner's
        /// turn. Fires <c>OnTurnStart</c> on each effect, decrements
        /// <see cref="IStatusEffect.RemainingTurns"/> for timed effects,
        /// and removes expired ones after firing
        /// <c>OnFinalAction</c>. Iterates backwards so removals do not
        /// corrupt the index.</summary>
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
        
        /// <summary>Fires end-of-turn hooks on all active effects. Operates
        /// on a defensive copy so that <c>OnTurnEnd</c> callbacks that
        /// add or remove effects do not invalidate the enumerator.
        /// Decrements remaining turns and removes expired effects via
        /// <see cref="Remove"/> (which also fires
        /// <c>OnRemove</c>).</summary>
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
        
        /// <summary>Pipes outgoing damage through every active effect's
        /// <c>OnDealDamage</c> hook, allowing each to modify
        /// <paramref name="damage"/> via <see langword="ref"/>.
        /// Effects iterate in insertion order; multiplicative stacking
        /// means order-agnostic results for flat modifiers but
        /// order-sensitive for percentage modifiers.</summary>
        /// <param name="target">The character receiving the damage.</param>
        /// <param name="damage">The damage value, modifiable by effects
        /// through the <see langword="ref"/> parameter.</param>
        public void OnDealDamage(Character target, ref int damage)
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnDealDamage(owner, target, ref damage);
            }
        }
        
        /// <summary>Pipes incoming damage through every active effect's
        /// <c>OnTakeDamage</c> hook. Called after
        /// <see cref="OnDealDamage"/> in the damage pipeline, so
        /// defensive modifiers stack on top of the attacker's
        /// amplified value.</summary>
        /// <param name="source">The character dealing the damage.</param>
        /// <param name="damage">The damage value, modifiable by effects
        /// through the <see langword="ref"/> parameter.</param>
        public void OnTakeDamage(Character source, ref int damage)
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnTakeDamage(owner, source, ref damage);
            }
        }
        
        /// <summary>Removes all active effects, firing <c>OnRemove</c> on
        /// each before clearing the list. Used when a character dies or
        /// an encounter ends so effects do not leak between battles.</summary>
        public void ClearAll()
        {
            foreach (var statusEffect in activeStatusEffectData)
            {
                statusEffect.Actions?.OnRemove(owner);
            }
            activeStatusEffectData.Clear();
        }
        
        /// <summary>Deep-clones the entire queue for a new character owner.
        /// Each effect is copied via <see cref="IStatusEffect.Copy"/>,
        /// and <c>OnApply</c> is re-fired on <paramref name="newOwner"/>
        /// so initialisation hooks run in the target's context. Used
        /// when a character is duplicated mid-encounter (e.g. a summon
        /// or mirror effect).</summary>
        /// <param name="newOwner">The character who will own the
        /// clone.</param>
        /// <returns>A new <see cref="StatusEffectQueue"/> bound to
        /// <paramref name="newOwner"/> with copies of all active
        /// effects.</returns>
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
