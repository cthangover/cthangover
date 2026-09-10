using System.Collections.Generic;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Per-mod data container inside a save file. Holds two "shelves":
    /// <list type="number">
    /// <item><b>Metadata</b> — structured metadata the core reads for
    /// validation before calling OnLoad.</item>
    /// <item><b>Data</b> — dictionary of named <see cref="DataBlob"/>
    /// blocks, each with its own version. Keys are defined by the mod.</item>
    /// </list>
    /// </summary>
    public class ModSlot
    {
        public ModMetadata Metadata { get; set; } = new();
        public Dictionary<string, DataBlob> Data { get; set; } = new();
    }
}
