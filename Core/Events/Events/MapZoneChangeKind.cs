namespace Cthangover.Core.Events
{
    /// <summary>
    /// String constants for <see cref="MapZoneChangedEvent.Change"/>. Plain
    /// strings instead of an enum on purpose: the same values cross the
    /// ModEventBus GDScript bridge as dictionary fields, and GDScript compares
    /// strings far more conveniently than enum ordinals.
    /// </summary>
    public static class MapZoneChangeKind
    {
        /// <summary>A zone was opened for travel.</summary>
        public const string Open = "open";

        /// <summary>A zone was closed for travel.</summary>
        public const string Close = "close";

        /// <summary>A zone was revealed on the map.</summary>
        public const string Show = "show";

        /// <summary>A zone was removed from the map.</summary>
        public const string Hide = "hide";

        /// <summary>A zone's overrides were dropped, returning it to authoring defaults.</summary>
        public const string Reset = "reset";

        /// <summary>Overrides were replaced wholesale, e.g. by a save load.</summary>
        public const string Restore = "restore";

        /// <summary>The raw zone list was re-read; every visual layer must rebuild.</summary>
        public const string Reload = "reload";
    }
}
