using System;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>Restores the caller’s progress snapshot when a suite exits normally, exceptionally or by cancellation.</summary>
    internal sealed class ModTestProgressScope : IDisposable
    {
        private readonly ModTestProgress previous = ModTestProgress.Current;

        /// <summary>
        /// Temporarily clears current progress while retaining the prior immutable snapshot for nested
        /// runner/error cleanup.
        /// </summary>
        public ModTestProgressScope()
        {
            ModTestProgress.Publish(null);
        }

        /// <summary>
        /// Restores the snapshot captured at scope construction, including exceptional or cancelled suite exit.
        /// </summary>
        public void Dispose()
        {
            ModTestProgress.Publish(previous);
        }
    }
}
