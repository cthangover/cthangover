using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// One collection strategy per entity kind, discovered by reflection like
    /// every other plugin point in the project (<c>Reflections.FindAndCreate</c>).
    /// The JSON entity walker (<see cref="JsonEntityStrategy"/>) scans every file
    /// once, reads the envelope's <c>"Kind"</c> and delegates the items to the
    /// strategy registered for that kind — so adding a new entity type means
    /// shipping one class, not editing a switch.
    /// <para/>
    /// A strategy fills <see cref="ValidationContext.Graph"/> with declarations
    /// and references for its kind. It must not throw on malformed data: missing
    /// ids are skipped silently, missing references are reported through the
    /// context.
    /// </summary>
    public interface IEntityKindStrategy
    {
        /// <summary>
        /// Entity kind this strategy handles — matches the <c>"Kind"</c> string
        /// in the JSON envelope and the <see cref="ValidationKind"/> used in the
        /// graph. Duplicate kinds across strategies are logged and the first
        /// discovery wins.
        /// </summary>
        string Kind { get; }

        /// <summary>
        /// Collects facts for one file of this strategy's kind. The caller has
        /// already verified the envelope shape and that the declared kind
        /// matches <see cref="Kind"/>.
        /// </summary>
        /// <param name="context">Validation pass state and report sink.</param>
        /// <param name="mod">Mod that owns the file.</param>
        /// <param name="file">Mod-relative file path, used as the declaration/reference source.</param>
        /// <param name="items">The envelope's <c>Items</c> array.</param>
        void Collect(ValidationContext context, IModInfo mod, string file, JsonElement items);
    }
}
