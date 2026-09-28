using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Extension point for gameplay subsystems that can be implemented in
    /// GDScript. <see cref="ModGDScriptLoader"/> walks every <c>.gd</c> file a mod
    /// declares and hands each compiled object to the registered hooks; a hook
    /// recognizes its own method signatures (for example
    /// <c>get_action_id</c>/<c>execute</c>) and wraps the script in a C# adapter.
    /// This keeps the generic loader free of subsystem-specific method names —
    /// the owning mod registers the hook when it loads.
    /// </summary>
    public interface IGDScriptHook
    {
        /// <summary>Stable id used to replace a hook when a mod reloads.</summary>
        string Id { get; }

        /// <summary>
        /// Inspects the compiled GDScript object and registers an adapter if the
        /// script implements this hook's contract. Called for every <c>.gd</c>
        /// file of every mod; implementations should simply return when their
        /// expected methods are absent.
        /// </summary>
        void TryRegister(GodotObject obj, string modId, string filePath);
    }
}
