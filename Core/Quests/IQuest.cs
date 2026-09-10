namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Read-only interface for quest state queries. Exposes the minimal set of
    /// properties that dialogue scripts, condition checkers, and UI bindings
    /// need to inspect a quest without being able to mutate it. Any concrete
    /// quest class that participates in the <see cref="QuestFactory"/> lookup
    /// system must implement this contract, and extension methods in
    /// <see cref="IQuestExtensions"/> provide convenience helpers layered on
    /// top of <c>ContainsTag</c> for common condition patterns.
    /// </summary>
    public interface IQuest
    {
        /// <summary>
        /// Unique string key that matches the quest's entry in the mod
        /// definition file and serves as the lookup key inside
        /// <see cref="QuestFactory"/>'s internal <c>Dictionary</c>.
        /// </summary>
        string ID { get; }

        /// <summary>
        /// Human-readable display name localised via
        /// <c>TranslationServer.Translate</c> before being shown in the
        /// journal window or notification banners.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current progression state of the quest, represented as one of the
        /// <see cref="QuestStatus"/> enum values. Dialogue condition nodes and
        /// journal formatting code use this to decide which description string
        /// to show and whether prerequisite gating should pass.
        /// </summary>
        QuestStatus Status { get; }

        /// <summary>
        /// Returns <c>true</c> if the quest's runtime tag set contains
        /// <paramref name="tag"/>. Tags are the primary mechanism for tracking
        /// sub-objectives during quest branches; a single quest accumulates
        /// tags as the player reaches milestones, and other systems (dialogue,
        /// scene transitions) query tags to gate content.
        /// </summary>
        bool ContainsTag(string tag);
    }
}
