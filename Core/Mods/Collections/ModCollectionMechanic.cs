using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One mechanic inside a <see cref="ModCollection"/> — a named role that a
    /// group of mods plays in the collection ("card battle", "novel interface",
    /// "developer tools"). Mechanics are declared by the collection, not by the
    /// mods: the same mod may be the backbone of one collection and an optional
    /// layer of another, so the role cannot live in the mod manifest.
    /// <para/>
    /// <see cref="Mods"/> entries use the same versioned string format as
    /// <c>manifest.json</c> dependencies (<c>"turn_battle_base&gt;=1"</c>). The
    /// optional suffix <c>"?"</c> is accepted but ignored — optionality of a
    /// whole mechanic is expressed by <see cref="Optional"/>.
    /// </summary>
    public class ModCollectionMechanic
    {
        /// <summary>Stable id unique within the collection; used by the graph view to group nodes.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Human-readable mechanic name shown in the graph frame header.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Short explanation of what the mechanic adds to the game.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Versioned mod references of this mechanic, same syntax as
        /// <c>manifest.json</c> <c>depends</c> (e.g. <c>"interface&gt;=1"</c>).
        /// </summary>
        [JsonPropertyName("mods")]
        public List<string> Mods { get; set; }

        /// <summary>
        /// True when the mechanic can be skipped without breaking the rest of the
        /// collection — the seed of the future game constructor. The resolver
        /// still includes optional mechanics in the graph and the install plan.
        /// </summary>
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }
    }
}
