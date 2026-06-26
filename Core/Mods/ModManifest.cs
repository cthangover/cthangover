using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for the <c>manifest.json</c> file required inside every
    /// mod. <c>Sources</c> declares glob-style paths to C# files that
    /// <c>ModCompiler</c> should compile; <c>Depends</c> lists other mod
    /// IDs that must load first (affects topological compile order);
    /// <c>Subscriptions</c> binds wrapper templates to scene lifecycle
    /// events so that mods can inject UI layers without patching scene
    /// files.
    /// </summary>
    public class ModManifest
    {
        /// <summary>Human-readable mod name (shown in UI).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Mod author credit string.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>Short description of what the mod does.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Glob-style paths to C# source files (e.g. "scripts/*.cs",
        /// "src/"). <c>ModCompiler</c> collects and compiles them.
        /// </summary>
        [JsonPropertyName("sources")]
        public List<string> Sources { get; set; }

        /// <summary>
        /// Mod IDs that must be loaded before this mod. Affects
        /// topological compile order and assembly reference resolution.
        /// </summary>
        [JsonPropertyName("depends")]
        public List<string> Depends { get; set; }

        /// <summary>
        /// Scene event bindings — each entry describes which wrapper
        /// template to inject into which scene, on which trigger.
        /// </summary>
        [JsonPropertyName("subscriptions")]
        public List<SubscriptionEntry> Subscriptions { get; set; }
    }
}
