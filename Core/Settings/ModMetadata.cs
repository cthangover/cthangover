namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Per-mod metadata stored alongside its data in the save file.
    /// Filled by the mod during <c>OnSave</c> and inspected by the core
    /// during the validation phase before <c>OnLoad</c>.
    ///
    /// <b>Required fields</b> (<c>ModId</c>, <c>ModName</c>,
    /// <c>MinCoreVersion</c>, <c>IsRequired</c>) are validated by the
    /// core.
    ///
    /// <b>Optional fields</b> (<c>ModVersion</c>, <c>CustomData</c>)
    /// are ignored by the core — they exist for the mod's own use.
    /// </summary>
    public class ModMetadata
    {
        /// <summary>Mod ID (duplicated here for self-verification).</summary>
        public string ModId { get; set; }

        /// <summary>Human-readable mod name shown in error messages.</summary>
        public string ModName { get; set; }

        /// <summary>Minimum core version required by this save.</summary>
        public string MinCoreVersion { get; set; }

        /// <summary>
        /// Historical flag kept for save-format compatibility. Loading no
        /// longer blocks on it: a missing mod's slot becomes dormant
        /// (<see cref="SaveDormancy"/>) so games survive composition changes.
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>Version of the mod that created this save (optional).</summary>
        public string ModVersion { get; set; }

        /// <summary>JSON string for any extra metadata the mod wants to store (optional).</summary>
        public string CustomData { get; set; }

        /// <summary>True while the slot is carried forward without its mod.</summary>
        public bool Dormant { get; set; }

        /// <summary>Why the slot is dormant (<see cref="SaveDormancy.Reason"/> name).</summary>
        public string DormantReason { get; set; }

        /// <summary>When the slot first became dormant.</summary>
        public System.DateTime? DormantSince { get; set; }
    }
}
