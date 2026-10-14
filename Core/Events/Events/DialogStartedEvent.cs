namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus when a dialog starts; mods subscribe by the <c>dialog_started</c> key.</summary>
    [EventKey("dialog_started")]
    public class DialogStartedEvent : ModEvent
    {
        /// <summary>Identifier of the dialog that started (declared; not yet published by the engine).</summary>
        public string DialogId { get; init; }
        /// <summary>Speaker identifier of the first line (declared; not yet published by the engine).</summary>
        public string Speaker { get; init; }
    }
}
