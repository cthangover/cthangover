namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published after an in-process mod-set reload finished (game switch or
    /// hot reload), once registries, locale and scenes were rebuilt and the
    /// released contexts were verified. <see cref="Applied"/> is false when
    /// the reload was rejected (compile errors) and the previous set still
    /// runs; <see cref="Leaked"/> names mods whose old code stayed in memory.
    /// </summary>
    [EventKey("mod_set_reloaded")]
    public class ModSetReloadedEvent : ModEvent
    {
        /// <summary>False when the reload was rejected and the previous set still runs.</summary>
        public bool Applied { get; init; }

        /// <summary>Mods that loaded into a new context.</summary>
        public string[] Loaded { get; init; }

        /// <summary>Mods whose context was released.</summary>
        public string[] Unloaded { get; init; }

        /// <summary>Released mods whose context survived collection.</summary>
        public string[] Leaked { get; init; }

        /// <summary>Compile errors of a rejected reload.</summary>
        public string[] Errors { get; init; }
    }
}
