using System;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// JSON DTO for item definitions in mod files. Uses
    /// <c>FlagsStringEnumConverter&lt;ItemType&gt;</c> to serialise the
    /// <c>[Flags]</c> enum as a human-readable comma list (e.g.
    /// <c>"Quest, Food"</c>) rather than a raw integer — this keeps mod
    /// JSON self-documenting and avoids bit-arithmetic errors when
    /// authors set multiple categories. The <c>Sprite</c> field is a
    /// path string resolved through <c>ItemSpriteFactory</c> at load
    /// time; the <c>ItemAction</c> field is an ID string resolved
    /// through <c>ItemActionFactory</c>.
    /// </summary>
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
