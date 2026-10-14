using System.Collections.Generic;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Stand-in for a character entity DTO in <c>JsonPatchMerger</c> tests.
    /// The real character DTO moved to the characters engine mod, which the
    /// test assembly cannot reference, so the merge tests exercise the same
    /// shape (scalar overrides plus <c>Actions$add</c>/<c>$remove</c> list
    /// operations) through this local sample.
    /// </summary>
    public class CharacterPatchSample
    {
        /// <summary>Entity id the patch is keyed by.</summary>
        public string Id { get; set; }
        /// <summary>Display name, overridden as a plain scalar in the merge tests.</summary>
        public string Name { get; set; }
        /// <summary>Sample scalar stat overridden by patches.</summary>
        public int Health { get; set; }
        /// <summary>Sample scalar stat overridden by patches.</summary>
        public int Attack { get; set; }
        /// <summary>Sample scalar stat overridden by patches.</summary>
        public int Point { get; set; }
        /// <summary>Sample list exercising the <c>$add</c>/<c>$remove</c> merge operations.</summary>
        public List<string> Actions { get; set; }
    }
}
