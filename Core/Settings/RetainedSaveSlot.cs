namespace Cthangover.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Generation-bound weak access to one retained Core record. Only detached emission is public: callers can
    /// merge ordinary data into their new save slot without modifying the rejected identity bytes or metadata.
    /// </summary>
    public sealed class RetainedSaveSlot
    {
        private readonly object generation;
        private readonly WeakReference<ModSlot> source;

        /// <summary>The original owner key, independent of optional or malformed metadata inside the raw slot.</summary>
        public string ModId { get; }

        internal RetainedSaveSlot(object generation, string modId, ModSlot slot)
        {
            this.generation = generation;
            ModId = modId;
            source = new WeakReference<ModSlot>(slot);
        }

        /// <summary>Requires the same generation and exact record still owned by the current Core dormant shelf.</summary>
        public bool IsCurrent
        {
            get
            {
                return source.TryGetTarget(out var slot) && SaveService.OwnsRetainedSlot(generation, ModId, slot);
            }
        }

        /// <summary>
        /// Copies only into the same owner's detached output slot. All allocations precede publication; blobs
        /// clone raw payload bytes without GetBytes/cache flushing. A stale token leaves the target unchanged.
        /// </summary>
        public bool TryCopyTo(ModSlot target)
        {
            if (target == null || target.Metadata?.ModId != ModId || !source.TryGetTarget(out var slot)
                || !SaveService.OwnsRetainedSlot(generation, ModId, slot))
            {
                return false;
            }

            var metadata = JsonSerializer.Deserialize<ModMetadata>(JsonSerializer.Serialize(slot.Metadata));
            Dictionary<string, DataBlob> data = null;
            if (slot.Data != null)
            {
                data = new Dictionary<string, DataBlob>(StringComparer.Ordinal);
                foreach (var pair in slot.Data)
                {
                    data.Add(pair.Key, pair.Value == null ? null : new DataBlob
                    {
                        Version = pair.Value.Version,
                        Payload = (byte[])pair.Value.Payload.Clone()
                    });
                }
            }

            if (!SaveService.OwnsRetainedSlot(generation, ModId, slot))
            {
                return false;
            }

            target.Metadata = metadata;
            target.Data = data;
            return true;
        }
    }
}
