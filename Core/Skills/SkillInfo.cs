using System;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Skills
{
    
    [Serializable]
    public class SkillInfo : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("Type")]
        public SkillType SkillType { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("Rare")]
        public RareType RareType { get; set; }
        
        private Texture2D cacheSprite;
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
