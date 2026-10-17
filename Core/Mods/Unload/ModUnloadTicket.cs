namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Godot;

    /// <summary>
    /// What an unload leaves behind: one weak reference per released mod
    /// context. Holding nothing strong is the whole point — the ticket must
    /// not be the reference that keeps a context alive. Collection is
    /// asynchronous in .NET (the runtime finishes an unload on a later GC once
    /// no frame, handle or type refers into the context), so verification is
    /// a separate, repeatable step: <see cref="Collect"/> right away (unit
    /// tests, synchronous reloads) or <see cref="CollectAsync"/> across a few
    /// engine frames (Godot releases the GC handles of freed objects at frame
    /// boundaries).
    /// </summary>
    public sealed class ModUnloadTicket
    {
        private readonly List<(string ModId, WeakReference Context)> contexts;

        /// <summary>Wraps the weak references taken by <see cref="ModUnloader"/> (null = nothing was released).</summary>
        public ModUnloadTicket(IEnumerable<(string ModId, WeakReference Context)> contexts)
        {
            this.contexts = contexts != null ? new List<(string, WeakReference)>(contexts) : new List<(string, WeakReference)>();
        }

        /// <summary>Ids of the mods whose contexts were released, in unload order.</summary>
        public IReadOnlyList<string> ModIds
        {
            get
            {
                var ids = new List<string>();
                foreach (var (modId, _) in contexts)
                    ids.Add(modId);
                return ids;
            }
        }

        /// <summary>Ids of the mods whose context is still alive right now.</summary>
        public IReadOnlyList<string> Alive
        {
            get
            {
                var alive = new List<string>();
                foreach (var (modId, context) in contexts)
                {
                    if (context.IsAlive)
                        alive.Add(modId);
                }
                return alive;
            }
        }

        /// <summary>Forces collections until every context died or <paramref name="attempts"/> ran out; returns the survivors.</summary>
        public IReadOnlyList<string> Collect(int attempts = 10)
        {
            for (var i = 0; i < attempts && Alive.Count > 0; i++)
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
            return Alive;
        }

        /// <summary>
        /// Like <see cref="Collect"/> but yields an engine frame between the
        /// attempts, so objects freed this frame release their handles first.
        /// Returns the survivors (empty = every context unloaded).
        /// </summary>
        public async Task<IReadOnlyList<string>> CollectAsync(SceneTree tree, int frames = 30, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < frames; i++)
            {
                if (Collect(attempts: 2).Count == 0)
                    return Array.Empty<string>();
                cancellationToken.ThrowIfCancellationRequested();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            return Alive;
        }
    }
}
