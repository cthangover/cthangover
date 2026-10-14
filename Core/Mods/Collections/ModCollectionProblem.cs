namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One inconsistency found by <see cref="ModCollectionResolver"/> while
    /// expanding a collection pyramid. Problems are runtime-only: they are
    /// recomputed on every resolve from the live registry and the collection
    /// store, and the scheme editor renders them on the "Проверка" tab.
    /// Fields carry enough context to jump to the offending element:
    /// (<see cref="ParentCollectionId"/>, <see cref="Ref"/>) for includes,
    /// <see cref="CollectionId"/> for requirements and <see cref="ModId"/> for
    /// mod-level issues. <see cref="GroupKey"/> matches
    /// <see cref="ModCollectionGroup.Key"/> when the problem belongs to a
    /// rendered group.
    /// </summary>
    public class ModCollectionProblem
    {
        /// <summary>Problem category; drives severity and the icon in the UI.</summary>
        public ModCollectionProblemKind Kind { get; set; }

        /// <summary>Short localized-ready title, e.g. "Блок «rpg_2d» не найден".</summary>
        public string Title { get; set; }

        /// <summary>Longer explanation with the fix hint.</summary>
        public string Detail { get; set; }

        /// <summary>Collection the problem belongs to (requirement owner, cycle node, role host).</summary>
        public string CollectionId { get; set; }

        /// <summary>Parent collection of a broken include; null for other kinds.</summary>
        public string ParentCollectionId { get; set; }

        /// <summary>Raw include reference of a broken include; null for other kinds.</summary>
        public string Ref { get; set; }

        /// <summary>Rendered group key when the problem can be highlighted on the canvas.</summary>
        public string GroupKey { get; set; }

        /// <summary>Mod id for mod-level problems; null otherwise.</summary>
        public string ModId { get; set; }
    }
}
