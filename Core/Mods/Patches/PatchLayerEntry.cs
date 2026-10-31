namespace Cthangover.Core.Mods.Patches
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;

    /// <summary>
    /// What a stored layer does to one property <c>K</c> of the value it is applied to, in a normal
    /// form <see cref="PatchLayerComposer"/> can extend with one more patch entry and write back:
    /// nothing; a concrete value (<c>K</c> with a scalar or array, <c>K: null</c>, <c>K$set</c>, or
    /// anything whose result no longer depends on the base); an object merged in (<c>K: {…}</c>, itself a
    /// layer of the property's type); or list operations, always as "remove these, then add these"
    /// (<c>K$remove</c> before <c>K$add</c>) — any order of removals and additions can be brought to it,
    /// see the composer.
    /// </summary>
    internal sealed class PatchLayerEntry
    {
        /// <summary>Which of the forms the entry has.</summary>
        public PatchLayerEntryKind Kind { get; set; }

        /// <summary>Concrete: the value after the layer; null with <see cref="SetForm"/> is a JSON <c>null</c>.</summary>
        public JsonNode Value { get; set; }

        /// <summary>Concrete: the property is removed (<c>K: null</c>).</summary>
        public bool Absent { get; set; }

        /// <summary>Concrete: written as <c>K$set</c> (always so for an object, which <c>K</c> would merge).</summary>
        public bool SetForm { get; set; }

        /// <summary>Fragment: the object merged into the property, a layer itself.</summary>
        public JsonObject Fragment { get; set; }

        /// <summary>List operations: elements removed first (null = no <c>K$remove</c>).</summary>
        public List<JsonNode> Removes { get; set; }

        /// <summary>List operations: elements added after the removals (null = no <c>K$add</c>, which also creates a missing list).</summary>
        public List<JsonNode> Adds { get; set; }

        /// <summary>List operations: the key of the explicit form <c>{"$key", "$items"}</c>, kept when written back.</summary>
        public string[] ExplicitKey { get; set; }
    }
}
