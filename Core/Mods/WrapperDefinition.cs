namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loaded wrapper template — a named UI tree definition stored as raw
    /// text content. Collected from <c>.wrappertmpl</c> files across mods
    /// by <c>ModManager.CollectWrapperTemplates</c> and later
    /// instantiated by the scene subscription system when a matching
    /// trigger fires. The content is kept as a raw string because it is
    /// parsed at instantiation time with the game's custom template
    /// format, not JSON.
    /// </summary>
    public class WrapperDefinition
    {
        /// <summary>The mod that owns this template.</summary>
        public string ModId { get; set; }

        /// <summary>Template name (filename minus .wrappertmpl extension).</summary>
        public string Name { get; set; }

        /// <summary>Raw template text — parsed at instantiation time.</summary>
        public string Content { get; set; }
    }
}
