namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;

    /// <summary>
    /// A DSL command other than <c>action</c> (<c>check</c>, <c>when</c>, <c>text</c>…): its
    /// positional arguments, its named ones and whether it jumps with <c>-&gt; :label</c>.
    /// </summary>
    public class CommandSchema
    {
        /// <summary>The command word.</summary>
        public string Id { get; set; }

        /// <summary>Locale key of the palette title.</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the help text.</summary>
        public string Description { get; set; }

        /// <summary>Palette group.</summary>
        public string Category { get; set; }

        /// <summary>Positional arguments in order (a quoted text, a stat id, a difficulty…).</summary>
        public List<FieldSchema> Positional { get; set; } = new();

        /// <summary>Named arguments.</summary>
        public List<FieldSchema> Params { get; set; } = new();

        /// <summary>Locale key naming the arrow target (<c>-&gt; :label</c>); null when the command takes none.</summary>
        public string Arrow { get; set; }

        /// <summary>The mod whose schema file won for this id (filled by the catalog).</summary>
        public string SourceMod { get; set; }
    }
}
