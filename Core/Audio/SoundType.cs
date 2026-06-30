namespace Cthangover.Core.Audio
{

    /// <summary>
    /// Sound category used as a pooling key. Each type gets its own
    /// AudioStreamPlayer, so a <c>CardEffect</c> sound cuts the previous
    /// card effect while a <c>UI</c> sound plays independently.
    /// <c>Timed</c> is intended for short-lived, one-shot events.
    /// </summary>
    public enum SoundType
    {
        /// <summary>Short-lived one-shot events; cut off by the next Timed sound.</summary>
        Timed,
        /// <summary>Long-running background effects; independent pool.</summary>
        Background,
        /// <summary>Foreground sounds that should not overlap with Background.</summary>
        Foreground,
        /// <summary>Interface clicks/hovers; isolated from game-world sounds.</summary>
        UI,
        /// <summary>Notification chimes; separate pool to avoid interrupting UI.</summary>
        Notification,
        /// <summary>Card ability VFX sounds; cuts previous card effects.</summary>
        CardEffect,
        /// <summary>Card action (play/discard) sounds; independent of CardEffect.</summary>
        CardAction,
    }

}
