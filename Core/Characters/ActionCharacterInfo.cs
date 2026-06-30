using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// JSON-serializable descriptor for an ActionCharacter. The Values list is a
    /// flat string array carrying the PropertyData pairs — serialized as a list
    /// rather than a dictionary to maintain JSON readability and avoid key
    /// escaping issues. Type uses a string enum converter so the JSON reads
    /// "ToSelf"/"ToAlias"/"ToEnemy" instead of integers. The Image field is a
    /// resource path string, resolved to Texture2D by the factory at load time.
    /// </summary>
    [Serializable]
    public class ActionCharacterInfo : IIdentifiable
    {
        /// <summary>
        /// Unique action identifier, serialized as "Id" in JSON.
        /// </summary>
        [JsonPropertyName("Id")]
        public string       ID { get; set; }
        /// <summary>
        /// Display name for the battle action menu.
        /// </summary>
        public string       Name { get; set; }
        /// <summary>
        /// Description text for the battle action tooltip.
        /// </summary>
        public string       Description { get; set; }
        /// <summary>
        /// Targeting category, serialized as string (e.g. "ToSelf",
        /// "ToAlias", "ToEnemy") via <see cref="JsonStringEnumConverter"/>
        /// for human-readable JSON.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ActionCharacterType Type { get; set; }
        /// <summary>
        /// Resource path string for the action icon. Resolved to
        /// <c>Texture2D</c> by <see cref="CharacterFactory"/> at load
        /// time.
        /// </summary>
        public string       Image { get; set; }
        /// <summary>
        /// Flat list of alternating key-value pairs that populate the
        /// <see cref="ActionCharacter.Properties"/> bag at load time.
        /// Index 0 = first key, index 1 = first value, index 2 = second
        /// key, etc. Stored as a list rather than a dictionary to
        /// simplify JSON authoring and avoid key-escaping issues.
        /// </summary>
        public List<string> Values { get; set; }
    }

}
