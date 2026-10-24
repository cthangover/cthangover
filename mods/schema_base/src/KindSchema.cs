namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;

    /// <summary>
    /// The editable fields of an entity kind (<c>"Kind": "stat"</c>, <c>"item"</c>…). Several
    /// mods may describe the same kind: their fields are merged, so the items mod describes
    /// price and picture, the equipment mod adds <c>Equip</c> and <c>Traits</c>, and the
    /// data editor window shows one form with all of them — the editor grows with the mods
    /// instead of being rewritten for each.
    /// </summary>
    public class KindSchema
    {
        /// <summary>The entity kind.</summary>
        public string Kind { get; set; }

        /// <summary>Locale key of the kind's name in the data editor window.</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the help text.</summary>
        public string Description { get; set; }

        /// <summary>Folder new entities are written to (<c>&lt;Folder&gt;/&lt;mod&gt;.json</c>).</summary>
        public string Folder { get; set; }

        /// <summary>Id a new entity starts from; <c>{mod}</c> is the mod id (<c>"{mod}/new_stat"</c>).</summary>
        public string NewId { get; set; }

        /// <summary>Order in the data editor window's kind list.</summary>
        public int Order { get; set; }

        /// <summary>The fields, in form order (merged across mods).</summary>
        public List<FieldSchema> Fields { get; set; } = new();
    }
}
