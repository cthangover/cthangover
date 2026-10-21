namespace Cthangover.Mods.Dialog.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Events;

    /// <summary>
    /// Records every <see cref="DialogEndedEvent"/> published on the mod event bus
    /// between <see cref="Start"/> and <see cref="Stop"/> — the locker id of each, in
    /// publication order. Tests count how often a queue reported its end.
    /// </summary>
    public class DialogEndRecorder
    {
        /// <summary>Locker ids of the recorded events (null for a locker-less dialog).</summary>
        public List<string> LockerIds { get; } = new();

        /// <summary>Subscribes to the event bus.</summary>
        public void Start()
        {
            ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);
        }

        /// <summary>Unsubscribes; call it in <c>finally</c> so a failed test leaves no handler behind.</summary>
        public void Stop()
        {
            ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
        }

        private void OnDialogEnded(DialogEndedEvent evt)
        {
            LockerIds.Add(evt.LockerId);
        }
    }
}
