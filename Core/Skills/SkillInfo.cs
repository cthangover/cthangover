using System;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Skills
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
        /// applied to the card frame by <see cref="SkillCardFrameBehaviour"/>.
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
