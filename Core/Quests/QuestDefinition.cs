using System.Collections.Generic;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Immutable data template loaded from a mod's JSON group file (stored
    /// under the <c>"quests"</c> key). Each definition carries the quest's
    /// identity, display name, and a mapping from
    /// <see cref="QuestStatus"/> integer values to localised description
    /// strings. <see cref="QuestFactory"/> reads these definitions once on
    /// initialisation and uses them to seed <see cref="QuestBase"/> runtime
    /// instances. The definition itself never changes during gameplay; all
    /// mutable progression lives in <see cref="QuestData"/>.
    /// Implements <see cref="IIdentifiable"/> so the factory can store
    /// quest definitions in a keyed collection.
    /// </summary>
    public class QuestDefinition : IIdentifiable
    {
        /// <summary>
        /// Unique string key used both as the JSON filename (minus
        /// extension) in the mod archive and as the lookup key inside
        /// <see cref="QuestFactory"/>'s internal dictionary.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Display name of the quest as written in the mod definition;
        /// passed through <c>TranslationServer.Translate</c> before being
        /// shown in the journal or notification banners.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Dictionary mapping integer status codes to localised description
        /// strings. During initialisation <see cref="QuestFactory"/> copies
        /// this dictionary into the corresponding
        /// <see cref="QuestBase.StatusToDescription"/> property of the
        /// runtime quest instance.
        /// </summary>
        public Dictionary<int, string> StatusToDescription { get; set; }
    }
}
