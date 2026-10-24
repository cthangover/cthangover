namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;

    /// <summary>
    /// One <c>schemas/*.json</c> file of a mod. Any mod may ship one, with no dependency on
    /// the schema tools: without them the file is simply never read.
    /// </summary>
    public class SchemaFile
    {
        /// <summary>Described scenario actions.</summary>
        public List<ActionSchema> Actions { get; set; } = new();

        /// <summary>Described DSL commands.</summary>
        public List<CommandSchema> Commands { get; set; } = new();

        /// <summary>Described entity kinds (fields merge across files).</summary>
        public List<KindSchema> Kinds { get; set; } = new();
    }
}
