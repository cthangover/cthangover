namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Characters;
    using Attribute = Cthangover.Core.Characters.Attribute;

    /// <summary>
    /// Writes bound stats (<see cref="StatInfo.Binds"/>) into a character's battle
    /// attributes. This is the whole integration with battles: the cores keep reading
    /// <c>Attributes.Attack</c> and <c>Attributes.Health</c> as they always did, and the
    /// numbers simply come from the stats now.
    /// <para/>
    /// Values are not reset: when max health grows from 30 to 36 (a level, a new armour)
    /// the current health grows by 6 too, so a wounded hero stays wounded; when the
    /// maximum drops (armour taken off) the current value is only capped; a fallen hero
    /// (health 0) is never revived by a sync. An attribute whose maximum was never set (0)
    /// is filled. Unbound attributes are left untouched, so a game may bind only some.
    /// </summary>
    public static class StatBindings
    {
        /// <summary>Attribute names a stat may bind to.</summary>
        public static readonly string[] Names = { "health", "attack", "defence", "point" };

        /// <summary>Applies the bound stats of <paramref name="sheet"/>; returns how many attributes changed.</summary>
        public static int Apply(StatSheet sheet, CharacterAttributes attributes)
            => Apply(sheet, attributes, StatsService.Definitions);

        /// <summary>Applies the stats of <paramref name="definitions"/> that bind an attribute.</summary>
        public static int Apply(StatSheet sheet, CharacterAttributes attributes, IEnumerable<StatInfo> definitions)
        {
            if (sheet == null || attributes == null || definitions == null)
                return 0;
            var changed = 0;
            foreach (var def in definitions.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Binds)))
            {
                var attribute = Find(attributes, def.Binds);
                if (attribute == null || !sheet.Has(def.ID))
                    continue;
                if (Move(attribute, Math.Max(0, sheet.Rounded(def.ID)), ReferenceEquals(attribute, attributes.Health)))
                    changed++;
            }
            return changed;
        }

        /// <summary>The attribute named <paramref name="name"/> (any case), or null for an unknown name.</summary>
        public static Attribute Find(CharacterAttributes attributes, string name)
        {
            return name?.Trim().ToLowerInvariant() switch
            {
                "health" or "hp" => attributes.Health,
                "attack" or "atk" => attributes.Attack,
                "defence" or "defense" or "def" => attributes.Defence,
                "point" or "points" or "ap" => attributes.Point,
                _ => null
            };
        }

        private static bool Move(Attribute attribute, int max, bool isHealth)
        {
            var oldMax = attribute.BaseValue;
            if (oldMax == max && attribute.Value <= max)
                return false;
            int value;
            if (oldMax <= 0)
                value = max;
            else if (isHealth && attribute.Value <= 0)
                value = attribute.Value;
            else if (max > oldMax)
                value = attribute.Value + (max - oldMax);
            else
                value = Math.Min(attribute.Value, max);
            attribute.BaseValue = max;
            attribute.Value = Math.Min(value, max);
            return true;
        }
    }
}
