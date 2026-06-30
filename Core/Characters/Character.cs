using System.Collections.Generic;
using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Factories;
using Godot;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Core character data model used in both the character factory and battle
    /// runtime. The Behaviour string is an extensibility hook — it names a
    /// behaviour class not defined here, resolved externally (likely by the AI
    /// or battle system). RecruitmentChance is the percentage for the recruit
    /// system. Copy() deep-clones Attributes and StatusEffectQueue with the new
    /// character reference — battle needs isolated copies so stat changes and
    /// status effects during combat don't mutate the template data.
    /// </summary>
    public class Character : IIdentifiable
    {
        /// <summary>
        /// Unique identifier string used as the factory lookup key and
        /// save-data reference. Must match the ID in the character's JSON
        /// data file (CharacterInfo.ID).
        /// </summary>
        public string             ID                { get; set; }
        /// <summary>
        /// Name of the behaviour class that controls this character's AI
        /// during battle. Resolved externally by the battle engine or AI
        /// system — not defined within this assembly. Null for player-
        /// controlled characters or simple enemies.
        /// </summary>
        public string             Behaviour         { get; set; }
        /// <summary>
        /// Current character level. Loaded from the template and carried
        /// forward into CharacterInfoData on recruitment.
        /// </summary>
        public int                Level             { get; set; }
        /// <summary>
        /// Current experience points. Accumulated through combat and used
        /// by the leveling system to determine when a level-up occurs.
        /// </summary>
        public int                Exp               { get; set; }
        /// <summary>
        /// Percentage chance (0–100) that a defeated enemy becomes
        /// recruitable via the recruit system. Read by battle resolution
        /// to determine if the "recruit" option appears post-combat.
        /// </summary>
        public int                RecruitmentChance { get; set; }
        /// <summary>
        /// Display name shown in the UI. Localized via TranslationServer
        /// when presented to the player (e.g. in party roster).
        /// </summary>
        public string             Name              { get; set; }
        /// <summary>
        /// Full-body character portrait texture for the character detail
        /// screen. Loaded from disk at factory construction time.
        /// </summary>
        public Texture2D          Image             { get; set; }
        /// <summary>
        /// Small portrait frame texture for list views (party roster,
        /// battle HUD). Typically a cropped or smaller version of Image.
        /// </summary>
        public Texture2D          Frame             { get; set; }
        /// <summary>
        /// List of battle actions available to this character. Drawn from
        /// the character's JSON data file and populated by the factory.
        /// </summary>
        public List<ActionCharacter>   Actions           { get; set; }
        /// <summary>
        /// Complete stat block: Health, Defence, Attack, Strength, Magic,
        /// Point (all <see cref="Attribute"/>-wrapped) plus raw Discipline,
        /// Depravity, and Fullness. Deep-copied by <see cref="Copy"/>.
        /// </summary>
        public CharacterAttributes     Attributes        { get; set; }
        /// <summary>
        /// Loot drop table for when this character is defeated as an
        /// enemy. Each <see cref="LootEntry"/> defines an item, count
        /// range, and drop probability.
        /// </summary>
        public List<LootEntry>   Loot              { get; set; }
        /// <summary>
        /// Queue of active status effects (buffs, debuffs, DoTs). Owned
        /// by this character — <see cref="Copy"/> passes a new owner
        /// reference so effect callbacks target the correct instance.
        /// </summary>
        public StatusEffectQueue  StatusEffectQueue { get; set; }

        /// <summary>
        /// Default constructor. Initializes Actions, Attributes, and
        /// StatusEffectQueue with empty/default collections to prevent
        /// null-reference errors in downstream code that iterates them.
        /// StatusEffectQueue receives <c>this</c> as its owner.
        /// </summary>
        public Character()
        {
            Actions           = new List<ActionCharacter>();
            Attributes        = new CharacterAttributes();
            StatusEffectQueue = new StatusEffectQueue(this);
        }
        
        /// <summary>
        /// Creates an independent deep copy of this character. All scalar
        /// fields are value-copied. <see cref="Attributes"/> and
        /// <see cref="StatusEffectQueue"/> are deep-cloned — this is
        /// critical for battle, where each combatant operates on an
        /// isolated copy so temporary stat changes and status effects
        /// don't leak back into the character template.
        /// </summary>
        public Character Copy()
        {
            var character = new Character()
            {
                ID                = ID,
                Behaviour         = Behaviour,
                Level             = Level,
                Exp               = Exp,
                Name              = Name,
                Image             = Image,
                Frame             = Frame,
                Actions           = Actions,
                RecruitmentChance = RecruitmentChance,
                Loot              = Loot,
                Attributes        = Attributes?.Copy(),
            };
            character.StatusEffectQueue = StatusEffectQueue?.Copy(character);
            return character;
        }
    }

}
