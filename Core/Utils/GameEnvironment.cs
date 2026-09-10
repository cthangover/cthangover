namespace Cthangover.Core.Utils
{
    public static class GameEnvironment
    {
        public static bool IsRuntime { get; set; }

        /// <summary>
        /// Managed thread id of the Godot main thread, captured once at startup
        /// (see <see cref="MarkMainThread"/>). Used by subsystems that may be
        /// invoked from background loading threads to decide whether a Godot
        /// API call is safe on the current thread.
        /// </summary>
        private static int _mainThreadId;

        /// <summary>
        /// True when the calling thread is the Godot main thread. Always true
        /// outside the runtime (unit tests) where no real Godot API is involved.
        /// </summary>
        public static bool IsMainThread =>
            !IsRuntime || _mainThreadId == 0 ||
            System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Captures the current thread as the Godot main thread. Called once from
        /// the static constructor of <c>GameData</c> (the first autoload), which
        /// is guaranteed to run on the main thread.
        /// </summary>
        public static void MarkMainThread()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
    }
}
