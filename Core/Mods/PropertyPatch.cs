using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    public class PropertyPatch : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtraProperties { get; set; }
    }
}
