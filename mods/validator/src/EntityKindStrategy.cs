using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Shared skeleton for <see cref="IEntityKindStrategy"/> implementations:
    /// iterates the envelope's items, resolves each entity id case-insensitively
    /// and records the declaration. Kinds that carry no extra structure (quests,
    /// skills, status effects) only override <see cref="Kind"/>; richer kinds
    /// override <see cref="CollectEntity"/> to add declared keys and references
    /// through <see cref="EntityFactCollector"/>.
    /// </summary>
    public abstract class EntityKindStrategy : IEntityKindStrategy
    {
        /// <inheritdoc />
        public abstract string Kind { get; }

        /// <inheritdoc />
        public void Collect(ValidationContext context, IModInfo mod, string file, JsonElement items)
        {
            foreach (var item in items.EnumerateArray())
            {
                context.ThrowIfCancellationRequested();

                var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                CollectEntity(context, mod, file, id, item);
            }
        }

        /// <summary>
        /// Records one entity. The default implementation adds the declaration
        /// under <see cref="Kind"/>, which doubles as the graph vocabulary
        /// (<see cref="ValidationKind"/>).
        /// </summary>
        protected virtual void CollectEntity(ValidationContext context, IModInfo mod, string file, string id, JsonElement item)
        {
            context.Graph.AddDeclaration(Kind, id, mod.Id, file, 0);
        }
    }
}
