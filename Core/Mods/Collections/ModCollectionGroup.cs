using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One contour of the resolved scheme: either an included child collection
    /// (<see cref="IsInclude"/>) or a leaf mechanic declared directly by the
    /// root collection. The scheme editor renders one group card per entry with
    /// <see cref="Mods"/> placed inside, and nested collections are flattened
    /// into the nearest group so the player sees a two-level pyramid
    /// (game → groups → mods) even when the documents nest deeper.
    /// <para/>
    /// <see cref="Key"/> is stable across resolutions (<c>"i:parent:ref"</c> or
    /// <c>"m:parent:mechanic"</c>) and is used as the persistency key for the
    /// layout store, so moving a group on the canvas survives reloads.
    /// </summary>
    public class ModCollectionGroup
    {
        /// <summary>Stable layout key: <c>"i:&lt;parentId&gt;:&lt;ref&gt;"</c> or <c>"m:&lt;parentId&gt;:&lt;mechanicId&gt;"</c>.</summary>
        public string Key { get; set; }

        /// <summary>True for a group produced by an <see cref="ModCollectionInclude"/>, false for a leaf mechanic.</summary>
        public bool IsInclude { get; set; }

        /// <summary>Collection shown inside the group (child collection or the owner of the mechanic).</summary>
        public string CollectionId { get; set; }

        /// <summary>Collection that declared the group (the include's parent).</summary>
        public string ParentCollectionId { get; set; }

        /// <summary>Owning mechanic id for leaf groups; null for includes.</summary>
        public string MechanicId { get; set; }

        /// <summary>Display name: include label, child collection name or mechanic name.</summary>
        public string Name { get; set; }

        /// <summary>Display description: include note, child collection or mechanic description.</summary>
        public string Description { get; set; }

        /// <summary>Slot role ("systems", "story", "ui", ...).</summary>
        public string Role { get; set; }

        /// <summary>True when the group may be toggled off.</summary>
        public bool Optional { get; set; }

        /// <summary>Whether the group participates in resolution (off optional groups stay visible but faded).</summary>
        public bool Selected { get; set; } = true;

        /// <summary>Mods inside the group, in declaration order, deduplicated by mod id.</summary>
        public List<ModCollectionModRef> Mods { get; set; } = new();
    }
}
