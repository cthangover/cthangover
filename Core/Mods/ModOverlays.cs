using System;
using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Content layers mounted on top of the loaded mod set without reloading it.
    /// An overlay is a plain <see cref="IModFileProvider"/> registered under an
    /// id that is not a mod id (e.g. the scenario studio's per-game edit layer
    /// in <c>user://studio/</c>); content resolvers that support overlays
    /// (<see cref="Resolvers.ScenarioResolver"/>,
    /// <see cref="Resolvers.SceneDefinitionResolver"/>) read them next to the
    /// mods, and overlay scenarios override mod scenarios of the same name.
    /// <para/>
    /// Mounting does not invalidate anything by itself: the owner calls the
    /// resolvers' invalidation after it changes overlay files, exactly as it
    /// would after editing a folder mod. A full mod reload keeps the overlays —
    /// they belong to the session, not to the mod set.
    /// </summary>
    public static class ModOverlays
    {
        private static readonly List<KeyValuePair<string, IModFileProvider>> overlays = new();

        /// <summary>Mounted overlays in mount order.</summary>
        public static IReadOnlyList<KeyValuePair<string, IModFileProvider>> All => overlays;

        /// <summary>Mounts <paramref name="provider"/> as overlay <paramref name="id"/>, replacing an overlay with the same id.</summary>
        public static void Mount(string id, IModFileProvider provider)
        {
            if (string.IsNullOrEmpty(id) || provider == null)
                throw new ArgumentException("overlay id and provider are required");

            Unmount(id);
            overlays.Add(new KeyValuePair<string, IModFileProvider>(id, provider));
        }

        /// <summary>Removes overlay <paramref name="id"/>; returns false when it was not mounted.</summary>
        public static bool Unmount(string id)
        {
            for (var i = 0; i < overlays.Count; i++)
            {
                if (overlays[i].Key != id)
                    continue;
                overlays.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Finds the provider of overlay <paramref name="id"/>.</summary>
        public static bool TryGet(string id, out IModFileProvider provider)
        {
            foreach (var overlay in overlays)
            {
                if (overlay.Key != id)
                    continue;
                provider = overlay.Value;
                return true;
            }

            provider = null;
            return false;
        }
    }
}
