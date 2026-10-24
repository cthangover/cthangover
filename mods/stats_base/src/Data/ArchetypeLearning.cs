namespace Cthangover.Mods.Stats
{
    /// <summary>A battle action an archetype teaches at a level (an RPG class's skill list).</summary>
    public class ArchetypeLearning
    {
        /// <summary>The level at which it is learned (1 = from the start).</summary>
        public int Level { get; set; } = 1;

        /// <summary>The id of the <c>"Kind": "action"</c> entity learned.</summary>
        public string Action { get; set; }
    }
}
