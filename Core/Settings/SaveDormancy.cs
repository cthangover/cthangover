using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Decides which mod slots of a save the running game restores and which
    /// stay dormant. A game keeps its saves while the player changes its
    /// mechanics, so a slot is never dropped: data of a mod that is not loaded
    /// now (or whose installed version is older than the save's) is carried
    /// forward verbatim in <see cref="SavePackage.Dormant"/> and revived as
    /// soon as the mod is back. Pure logic — the version lookup is injected —
    /// so it is covered by unit tests without Godot.
    /// </summary>
    public static class SaveDormancy
    {
        public enum Reason
        {
            /// <summary>The mod is not part of the game's current composition.</summary>
            NotLoaded,

            /// <summary>The installed mod is older than the one that wrote the slot (no downgrade migration).</summary>
            Downgrade,

            /// <summary>The slot needs a newer core than the running one.</summary>
            CoreTooOld,

            /// <summary>The mod rejected the slot in <c>OnLoad</c>; the data is kept for a later fix.</summary>
            LoadFailed,
        }

        public sealed class Result
        {
            /// <summary>Slots to hand to the loaded mods, keyed by mod id.</summary>
            public Dictionary<string, ModSlot> Live { get; } = new();

            /// <summary>Slots carried forward untouched, keyed by mod id.</summary>
            public Dictionary<string, ModSlot> Dormant { get; } = new();

            /// <summary>Slots that just became dormant, with the reason.</summary>
            public List<(string ModId, Reason Reason)> NewlyDormant { get; } = new();

            /// <summary>Previously dormant slots whose mod is back.</summary>
            public List<string> Revived { get; } = new();
        }

        /// <summary>
        /// Classifies <paramref name="modData"/> (live slots of the save, core
        /// slot excluded) and <paramref name="dormant"/> (slots already dormant
        /// in the save). <paramref name="installedVersion"/> returns the loaded
        /// mod's version or null when the mod is not loaded.
        /// </summary>
        public static Result Classify(
            IReadOnlyDictionary<string, ModSlot> modData,
            IReadOnlyDictionary<string, ModSlot> dormant,
            Func<string, string> installedVersion,
            string coreVersion,
            DateTime now)
        {
            var result = new Result();

            foreach (var pair in modData ?? new Dictionary<string, ModSlot>())
            {
                if (pair.Key == SavePackage.CoreSlotKey || pair.Value == null)
                    continue;

                var reason = Check(pair.Key, pair.Value, installedVersion, coreVersion);
                if (reason == null)
                {
                    result.Live[pair.Key] = pair.Value;
                    continue;
                }

                result.Dormant[pair.Key] = MarkDormant(pair.Value, reason.Value, now);
                result.NewlyDormant.Add((pair.Key, reason.Value));
            }

            foreach (var pair in dormant ?? new Dictionary<string, ModSlot>())
            {
                if (pair.Value == null || result.Dormant.ContainsKey(pair.Key))
                    continue;

                // A live slot for the same mod wins: the dormant one is kept
                // (it may hold newer data after a downgrade) until purged.
                if (result.Live.ContainsKey(pair.Key) || Check(pair.Key, pair.Value, installedVersion, coreVersion) != null)
                {
                    result.Dormant[pair.Key] = pair.Value;
                    continue;
                }

                Revive(pair.Value);
                result.Live[pair.Key] = pair.Value;
                result.Revived.Add(pair.Key);
            }

            return result;
        }

        /// <summary>Flags a slot as dormant, keeping the first dormancy date.</summary>
        public static ModSlot MarkDormant(ModSlot slot, Reason reason, DateTime now)
        {
            slot.Metadata ??= new ModMetadata();
            slot.Metadata.Dormant = true;
            slot.Metadata.DormantReason = reason.ToString();
            slot.Metadata.DormantSince ??= now;
            return slot;
        }

        private static void Revive(ModSlot slot)
        {
            if (slot.Metadata == null)
                return;
            slot.Metadata.Dormant = false;
            slot.Metadata.DormantReason = null;
            slot.Metadata.DormantSince = null;
        }

        private static Reason? Check(string modId, ModSlot slot, Func<string, string> installedVersion, string coreVersion)
        {
            var installed = installedVersion?.Invoke(modId);
            if (installed == null)
                return Reason.NotLoaded;

            var meta = slot.Metadata;
            if (!string.IsNullOrEmpty(meta?.ModVersion) && Compare(meta.ModVersion, installed) > 0)
                return Reason.Downgrade;

            if (!string.IsNullOrEmpty(meta?.MinCoreVersion) && !string.IsNullOrEmpty(coreVersion)
                && Compare(coreVersion, meta.MinCoreVersion) < 0)
                return Reason.CoreTooOld;

            return null;
        }

        private static int Compare(string a, string b)
        {
            try
            {
                return VersionHelper.Compare(a, b);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
