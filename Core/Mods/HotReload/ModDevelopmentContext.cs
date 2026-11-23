namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Godot;

    /// <summary>Game-thread command context. Borrow it only for the lifetime of the executing strategy.</summary>
    public sealed class ModDevelopmentContext
    {
        internal ModDevelopmentContext(SceneTree tree, JsonObject request)
        {
            Tree = tree;
            Request = request;
        }

        /// <summary>The active game tree.</summary>
        public SceneTree Tree { get; }

        /// <summary>The exact parsed command and its arguments.</summary>
        public JsonObject Request { get; }

        /// <summary>True when the strategy succeeded. Rejections can supply structured diagnostics as their result.</summary>
        public bool Succeeded { get; set; } = true;

        internal bool Changed { get; private set; }

        /// <summary>Marks that the command committed a change, so the response advances the process revision.</summary>
        public void MarkChanged() => Changed = true;

        /// <summary>Reads a required nonempty string argument.</summary>
        public string Require(string field)
        {
            var value = Request[field]?.GetValue<string>();
            return !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing " + field + ".");
        }

        internal string ModId(bool loaded = false)
        {
            var id = Require("mod");
            if (id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
                throw new ArgumentException("Invalid mod identity.");
            if (loaded && !ModRegistry.Instance.Mods.ContainsKey(id))
                throw new ArgumentException("Mod is not active: " + id);
            return id;
        }
    }
}
