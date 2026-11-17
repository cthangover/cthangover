namespace Cthangover.Core.Settings
{
    using System;

    /// <summary>
    /// Read-only classification for the currently executing load, including owner rejections before Joined.
    /// It never exposes mutable slot dictionaries and cannot be mistaken for a previous load's report.
    /// </summary>
    public sealed class SaveLoadContext
    {
        private readonly object generation;
        private readonly SaveDormancy.Result source;

        internal SaveLoadContext(object generation, SaveDormancy.Result source)
        {
            this.generation = generation;
            this.source = source;
        }

        /// <summary>False as soon as the owning ApplyPackage leaves its finally block.</summary>
        public bool IsActive
        {
            get
            {
                return ReferenceEquals(SaveService.ApplyingDormancy, this);
            }
        }

        /// <summary>Reports the current live shelf; a retained context refuses rather than reporting stale absence.</summary>
        public bool HasLiveSlot(string modId)
        {
            RequireActive();
            return source.Live.ContainsKey(modId);
        }

        /// <summary>Reports both composition dormancy and rejection by an owner in this dispatch.</summary>
        public bool HasDormantSlot(string modId)
        {
            RequireActive();
            return source.Dormant.ContainsKey(modId);
        }

        /// <summary>
        /// Borrows the exact Core-owned dormant record through an opaque weak token. It retains no copied payload
        /// or owner graph; new-game, another load or replacement of that record makes the token stale.
        /// </summary>
        public bool TryRetainDormantSlot(string modId, out RetainedSaveSlot retained)
        {
            RequireActive();
            retained = null;
            if (!source.Dormant.TryGetValue(modId, out var slot))
            {
                return false;
            }

            retained = new RetainedSaveSlot(generation, modId, slot);
            return true;
        }

        internal bool MatchesDormant(string modId, ModSlot slot)
        {
            return source.Dormant.TryGetValue(modId, out var current) && ReferenceEquals(current, slot);
        }

        private void RequireActive()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("Save load context is no longer active");
            }
        }
    }
}
