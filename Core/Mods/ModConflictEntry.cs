namespace Cthangover.Core.Mods
{
    /// <summary>
    /// A record of a conflict between two mods over a single resource.
    /// </summary>
    public class ModConflictEntry
    {
        /// <summary>"character_property", "item_property", "texture", "interactive", "scene", "file"</summary>
        public string ResourceType { get; set; }

        /// <summary>"Marao.Skills", "sword.png", "home_kitchen"</summary>
        public string ResourceId { get; set; }

        /// <summary>The mod whose value became final.</summary>
        public string WinningModId { get; set; }

        /// <summary>The mod whose value was overridden.</summary>
        public string OverriddenModId { get; set; }

        /// <summary>true if arrays were concatenated, false if full replacement.</summary>
        public bool WasConcatenated { get; set; }

        /// <summary>Optional explanation.</summary>
        public string Note { get; set; }
    }
}
