using System.Collections.Generic;

namespace Cthangover.Core.UI.Messages
{
    /// <summary>
    /// Static buffer that queues notification texts before they are displayed.
    /// Notifications accumulate here and wait until a visible slot becomes
    /// available in <see cref="NotificationPanel"/>. The 10-second display
    /// timer starts only when a notification is pulled from the buffer and
    /// shown on screen — pending entries in the buffer have no timeout.
    /// </summary>
    public static class NotificationBuffer
    {
        private static readonly Queue<string> _pending = new();

        /// <summary>
        /// Enqueues a notification text into the buffer. Null or empty
        /// strings are silently ignored. Notifications are served in FIFO
        /// order by <see cref="TryDequeue"/>.
        /// </summary>
        /// <param name="text">The notification text to enqueue.</param>
        public static void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            _pending.Enqueue(text);
        }

        /// <summary>
        /// Dequeues the oldest pending notification, if any.
        /// </summary>
        /// <param name="text">Receives the dequeued text, or <c>null</c> if the buffer is empty.</param>
        /// <returns><c>true</c> if a notification was dequeued; <c>false</c> otherwise.</returns>
        public static bool TryDequeue(out string text)
        {
            if (_pending.Count > 0)
            {
                text = _pending.Dequeue();
                return true;
            }
            text = null;
            return false;
        }

        /// <summary>
        /// The number of notifications currently waiting in the buffer.
        /// </summary>
        public static int PendingCount => _pending.Count;

        /// <summary>
        /// Removes all pending notifications from the buffer without
        /// displaying them. Safe to call at any time.
        /// </summary>
        public static void Clear()
        {
            _pending.Clear();
        }
    }
}
