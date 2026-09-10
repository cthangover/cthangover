using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Parses a single section type in a .tscn file (e.g. [node], [ext_resource]).
    /// Each implementation is responsible for exactly one section type.
    /// <para/>
    /// Lifecycle: Open (section header) → Property* (zero or more property lines) → Close (commit to ParsedScene).
    /// The orchestrator (<see cref="TscnParser"/>) calls these methods as it walks token stream,
    /// never branching on section type itself — lookup is deferred to <see cref="SectionParserRegistry"/>.
    /// <para/>
    /// Auto-discovered via <c>Reflections.FindAndCreate&lt;ISectionParser&gt;()</c>.
    /// To add a new section type, implement this interface in any loaded assembly — no registration code needed.
    /// </summary>
    public interface ISectionParser
    {
        /// <summary>Section type name as it appears in .tscn (e.g. "node", "ext_resource").</summary>
        string SectionType { get; }

        /// <summary>Called when a section header is encountered. <paramref name="headerAttrs"/> are the parsed key=value attributes from the brackets.</summary>
        void Open(Dictionary<string, string> headerAttrs);

        /// <summary>Called for each key=value property line under the current section.</summary>
        void Property(string key, string rawValue);

        /// <summary>Called when the next section header is encountered (or at end of file). Commits all accumulated state to <paramref name="scene"/>.</summary>
        void Close(ParsedScene scene);
    }
}

