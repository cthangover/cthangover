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
        [JsonPropertyName("Id")]
        public string       ID { get; set; }
        public string       Name { get; set; }
        public string       Description { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ActionCharacterType Type { get; set; }
        public string       Image { get; set; }
        public List<string> Values { get; set; }
    }

}
