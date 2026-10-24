namespace Cthangover.Mods.BattleFormulas
{
    using System;
    using Cthangover.Core.Cards.StatusEffect;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Stats;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>
    /// Runs a battle action whose numbers are data. An <c>"Kind": "action"</c> entity with
    /// these values needs no C# of its own:
    /// <list type="bullet">
    /// <item><c>Formula=</c> the amount (<c>a.mag * 3 - b.mdf + rand(0, 4)</c>), never below 0;</item>
    /// <item><c>Effect=damage</c> (default) or <c>heal</c>;</item>
    /// <item><c>Cost=mp:5</c> spends a pool stat of a party member (enemies pay nothing);
    ///   <c>RequiredPoint=</c> spends action points as every core does;</item>
    /// <item><c>Pierce=1</c> ignores the target's shield (the consumable defence attribute
    ///   the cards build up), otherwise the shield absorbs first, as in both cores;</item>
    /// <item><c>State=effect/physics/stun</c> (+ <c>StateTurns=</c>, <c>StateChance=</c> percent) puts a
    ///   status effect on the target.</item>
    /// </list>
    /// Status effect hooks see the damage like any core's damage, so passives and statuses
    /// of other mods keep working. Executors are registered globally, which is why the same
    /// skill works in the FF core and in the card core: each core's own provider is asked
    /// first and falls back to the global ones for ids it does not know.
    /// </summary>
    public class FormulaActionExecutor : ActionBase
    {
        private readonly string id;

        /// <summary>Executor of the action entity <paramref name="actionId"/>.</summary>
        public FormulaActionExecutor(string actionId)
        {
            id = actionId;
        }

        /// <inheritdoc />
        public override string ID => id;

        /// <inheritdoc />
        public override ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (user == null || action == null)
                return new ChangedAttributes { Result = false };
            if (target == null || action.Type == ActionCharacterType.ToSelf)
                target = user;
            // The pool is only checked first and spent after the action points, so a refused
            // action never costs mana.
            if (!PayCost(action, user, false) || !CheckRequiredAndUsePoint(action, user))
                return new ChangedAttributes { Result = false };
            PayCost(action, user, true);

            var amount = Math.Max(0, (int)Math.Round(Formula.Evaluate(action.GetStr("Formula"), new BattleFormulaScope(user, target)), MidpointRounding.AwayFromZero));
            var result = string.Equals(action.GetStr("Effect"), "heal", StringComparison.OrdinalIgnoreCase)
                ? Heal(target, amount)
                : Damage(action, user, target, amount);
            ApplyState(action, target);
            GameLogger.Log("BATTLE", $"formula action '{id}': {user.ID} -> {target.ID}, amount {amount}, damage {result.Target.Damage}", LogLevel.Debug);
            return result;
        }

        private static ChangedAttributes Damage(ActionCharacter action, Character user, Character target, int amount)
        {
            StatusEffectService.Of(target).OnTakeDamage(user, ref amount);
            StatusEffectService.Of(user).OnDealDamage(target, ref amount);

            var shield = 0;
            var pierce = action.GetInt("Pierce") != 0 || user.HasPassiveEffect("IgnoreArmor");
            if (!pierce && target.Attributes.Defence.Value > 0)
            {
                shield = Math.Min(target.Attributes.Defence.Value, amount);
                amount -= shield;
                target.Attributes.Defence.Value -= shield;
            }
            target.Attributes.Health.Value = Math.Max(0, target.Attributes.Health.Value - amount);
            return new ChangedAttributes { Result = true, Target = { Damage = amount, Defence = -shield } };
        }

        private static ChangedAttributes Heal(Character target, int amount)
        {
            var health = target.Attributes.Health;
            var healed = Math.Max(0, Math.Min(amount, health.BaseValue - health.Value));
            health.Value += healed;
            return new ChangedAttributes { Result = true, Target = { Damage = healed } };
        }

        private static bool PayCost(ActionCharacter action, Character user, bool spend)
        {
            var cost = action.GetStr("Cost");
            if (string.IsNullOrWhiteSpace(cost) || StatsService.RosterEntry(user.ID) == null)
                return true;
            var colon = cost.IndexOf(':');
            if (colon <= 0 || !int.TryParse(cost.Substring(colon + 1), out var amount))
            {
                if (!spend)
                    GameLogger.Log("BATTLE", $"action '{action.ID}': Cost must look like 'mp:5', got '{cost}'", LogLevel.Error);
                return true;
            }
            var stat = cost.Substring(0, colon).Trim();
            if (spend)
                return StatPools.Spend(user.ID, stat, amount);
            if (StatPools.Current(user.ID, stat) >= amount)
                return true;
            GameLogger.Log("BATTLE", $"{user.ID} lacks {stat} for '{action.ID}' ({StatPools.Current(user.ID, stat)} < {amount})", LogLevel.Warning);
            return false;
        }

        private static void ApplyState(ActionCharacter action, Character target)
        {
            var state = action.GetStr("State");
            if (string.IsNullOrWhiteSpace(state) || target.Attributes.Health.Value <= 0)
                return;
            var chance = action.Properties.Values.ContainsKey("StateChance") ? action.GetInt("StateChance") : 100;
            if (Random.Shared.Next(100) >= chance)
                return;
            var turns = action.Properties.Values.ContainsKey("StateTurns") ? action.GetInt("StateTurns") : -1;
            StatusEffectService.Of(target).Add(state.Trim(), turns);
        }
    }
}
