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
        public string             ID                { get; set; }
        public string             Behaviour         { get; set; }
        public int                Level             { get; set; }
        public int                Exp               { get; set; }
        public int                RecruitmentChance { get; set; }
        public string             Name              { get; set; }
        public Texture2D          Image             { get; set; }
        public Texture2D          Frame             { get; set; }
        public List<ActionCharacter>   Actions           { get; set; }
        public CharacterAttributes     Attributes        { get; set; }
        public List<LootEntry>   Loot              { get; set; }
        public StatusEffectQueue  StatusEffectQueue { get; set; }

        public Character()
        {
            Actions           = new List<ActionCharacter>();
            Attributes        = new CharacterAttributes();
            StatusEffectQueue = new StatusEffectQueue(this);
        }
        
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
