namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;

    /// <summary>
    /// A scenario action (<c>action &lt;id&gt; key=value …</c>) as data: what it does and what
    /// it takes. The mod that implements the action ships its schema next to the code, but
    /// anyone may describe an action — the schema tools describe the engine's own and other
    /// mods' actions this way, like type definitions for a library that has none.
    /// </summary>
    public class ActionSchema
    {
        /// <summary>The action name after <c>action</c> (<c>stat.add</c>).</summary>
        public string Id { get; set; }

        /// <summary>Locale key of the palette title.</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the help text.</summary>
        public string Description { get; set; }

        /// <summary>Free group name the palette sorts by (<c>stats</c>, <c>battle</c>, <c>items</c>).</summary>
        public string Category { get; set; }

        /// <summary>Named parameters, in form order.</summary>
        public List<FieldSchema> Params { get; set; } = new();

        /// <summary>The mod whose schema file won for this id (filled by the catalog).</summary>
        public string SourceMod { get; set; }
    }
}
