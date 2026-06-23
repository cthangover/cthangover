using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Characters
{

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
