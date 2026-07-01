using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// A battle action available to a character (e.g. "Attack", "Heal"). Carries
    /// a PropertyData bag keyed by well-known string constants (Attack, Defence,
    /// Heal, Turn, RequiredPoint) rather than typed properties — this allows
    /// mods to add custom action parameters without schema changes. Copy()
    /// deep-clones Properties to prevent shared state between character instances
    /// that may modify action data at runtime (e.g. temporary buffs).
    /// </summary>
    public class ActionCharacter : IIdentifiable
    {
        /// <summary>
        /// Unique action identifier, used as the factory lookup key.
        /// </summary>
        public string              ID          { get; set; }
        /// <summary>
        /// Display name shown in the battle action menu.
        /// </summary>
        public string              Name        { get; set; }
        /// <summary>
        /// Tooltip/description text shown in the battle action menu.
        /// </summary>
        public string              Description { get; set; }
        /// <summary>
        /// Targeting category: <see cref="ActionCharacterType.ToSelf"/>
        /// (caster), <see cref="ActionCharacterType.ToAlias"/> (allies),
        /// or <see cref="ActionCharacterType.ToEnemy"/> (opponents).
        /// </summary>
        public ActionCharacterType Type        { get; set; }
        /// <summary>
        /// Icon texture for the battle action menu.
        /// </summary>
        public Texture2D           Image       { get; set; }
        /// <summary>
        /// String-indexed property bag containing the action's numeric
        /// parameters (damage, heal, cost, etc.). Uses well-known keys
        /// (e.g. <see cref="ATTRIBUTE_ATTACK"/>) defined as constants on
        /// this class. Mods can add custom keys without schema changes.
        /// </summary>
        public PropertyData        Properties  { get; set; }

        /// <summary>
        /// Reads a string property from <see cref="Properties"/> by key,
        /// with an optional default for missing entries.
        /// </summary>
        public string GetStr(string name, string defaultValue = null)
            => Properties.GetStr(name, defaultValue);
        
        /// <summary>
        /// Reads an integer property from <see cref="Properties"/> by key,
        /// with an optional default for missing entries.
        /// </summary>
        public int GetInt(string name, int defaultValue = 0)
            => Properties.GetInt(name, defaultValue);
        
        /// <summary>
        /// Reads a float property from <see cref="Properties"/> by key,
        /// with an optional default for missing entries.
        /// </summary>
        public float GetFloat(string name, float defaultValue = 0f)
            => Properties.GetFloat(name, defaultValue);
        
        /// <summary>
        /// Reads a boolean property from <see cref="Properties"/> by key.
        /// </summary>
        public bool GetBool(string name)
            => Properties.GetBool(name);
        
        /// <summary>
        /// Creates an independent copy of this action. All scalar/string
        /// fields are value-copied; <see cref="Properties"/> is deep-
        /// cloned via <see cref="PropertyData.Clone"/> to prevent shared
        /// state mutations (e.g. temporary effect modifiers) from
        /// leaking across character instances.
        /// </summary>
        public ActionCharacter Copy()
        {
            return new ActionCharacter()
            {
                ID          = ID,
                Name        = Name,
                Description = Description,
                Type        = Type,
                Image       = Image,
                Properties  = Properties?.Clone(),
            };
        }
    }

}
