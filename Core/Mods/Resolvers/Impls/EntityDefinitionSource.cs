namespace Cthangover.Core.Mods.Resolvers
{
    using System.Text.Json.Nodes;

    /// <summary>The winning definition of one entity: its JSON as written and where it lives.</summary>
    public sealed class EntityDefinitionSource
    {
        /// <summary>Mod that defines the entity (the last one in load order when several do).</summary>
        public string ModId { get; init; }

        /// <summary>Mod-relative data file.</summary>
        public string File { get; init; }

        /// <summary>The item exactly as written in the file (may hold <c>Extends</c> and <c>Key$add</c> operations).</summary>
        public JsonObject Written { get; init; }
    }
}
