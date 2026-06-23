using System;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Items
{
    [Serializable]
    public class ItemInfo : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int    Cost { get; set; }
        public string Sprite { get; set; }
        [JsonConverter(typeof(FlagsStringEnumConverter<ItemType>))]
        public ItemType ItemType { get; set; }
        public string ItemAction { get; set; }
    }
}
