namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus when a dialog finishes; mods subscribe by the <c>dialog_ended</c> key.</summary>
    [EventKey("dialog_ended")]
    public class DialogEndedEvent : ModEvent
    {
        /// <summary>Identifier of the finished dialog. The engine currently publishes this event without a payload.</summary>
        public string DialogId { get; init; }

        /// <summary>
        /// Identifier of the ExecutableEvent that locked the dialog box while the
        /// queue ran (its <c>ID</c>), or null when the dialog ran without a locker.
        /// Event chains compare it against their last-run event to break
        /// self-trigger loops.
        /// </summary>
        public string LockerId { get; init; }
    }
}
