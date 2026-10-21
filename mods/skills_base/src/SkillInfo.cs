using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Skills
{
    
    /// <summary>
    /// Immutable data model for a single skill definition, populated by
    /// <see cref="SkillFactory"/> from JSON assets. Each instance carries the
    /// metadata required to render a skill card in the UI (<see cref="Sprite"/>,
    /// <see cref="Name"/>, <see cref="Description"/>) and to resolve its gameplay
    /// behaviour through <see cref="SkillType"/> and <see cref="RareType"/>.
    /// Instances are shared across the runtime and should not be mutated after
    /// construction.
    /// </summary>
    [Serializable]
    [EntityKind("skill")]
    public class SkillInfo : IIdentifiable
    {
        /// <summary>
        /// The unique string key of this skill, used as the primary lookup in
        /// <see cref="SkillFactory"/> and as the identity token in
        /// <see cref="SkillData.Skills"/>.
        /// </summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>
        /// The display name shown on the skill card. This value is passed through
        /// <see cref="TranslationServer.Translate"/> before rendering, allowing
        /// localisation via Godot's translation system.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A longer textual description explaining what the skill does in gameplay
        /// terms. Displayed in tooltip and detail views alongside the card artwork.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The relative path to the skill's artwork texture, resolved inside the
        /// mod-group "characters/skills" by <see cref="TextureUtils.LoadFromModGroup"/>
        /// when the <see cref="Sprite"/> property is first accessed.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Optional ID of the action card this skill gifts to the player when
        /// acquired (e.g. an active skill that hands over a battle card).
        /// Resolved against <see cref="Cthangover.Core.Factories.Impls.ActionCharacterFactory"/>
        /// when the skills panel renders the detail view, and pushed into
        /// <see cref="Cthangover.Core.Settings.ActionPoolData.ActionIds"/> by
        /// <see cref="SkillService"/> so the card becomes assignable to a
        /// character. Empty for skills that only act as passive gates and
        /// never produce a card.
        /// </summary>
        public string GrantsAction { get; set; }

        /// <summary>
        /// Numeric bonuses this skill grants while owned. They are the card's
        /// actual payload: the skills panel shows the first effect on the grid
        /// card, lists all of them in the study plate, and sums every owned
        /// skill's effects into the global "collection effect" plate via
        /// <see cref="SkillEffectAggregator"/>. Empty for skills that only gate
        /// content (e.g. unlock an action) without touching stats.
        /// </summary>
        [JsonPropertyName("Effects")]
        public List<SkillEffectInfo> Effects { get; set; }
        
        /// <summary>
        /// The gameplay classification of this skill (<c>Active</c>, <c>Passive</c>,
        /// or <c>ForCharacter</c>), serialised as a string in JSON and converted
        /// via <see cref="JsonStringEnumConverter"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("Type")]
        public SkillType SkillType { get; set; }
        
        /// <summary>
        /// The rarity tier of this skill, serialised as a string in JSON and
        /// converted via <see cref="JsonStringEnumConverter"/>. Drives the colour
        /// of the card frame, study plate and rarity badge through
        /// <see cref="SkillPalette"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("Rare")]
        public RareType RareType { get; set; }
        
        private Texture2D cacheSprite;

        /// <summary>
        /// Lazily loads and caches the <see cref="Texture2D"/> for this skill's
        /// card artwork. On first access the texture is resolved from the mod-group
        /// "characters/skills" using the <see cref="Image"/> path; subsequent
        /// accesses return the cached reference.
        /// </summary>
        public Texture2D Sprite
        {
            get
            {
                if (cacheSprite == null)
                    cacheSprite = TextureUtils.LoadFromModGroup("characters/skills", Image);
                return cacheSprite;
            }
        }
    }
}
