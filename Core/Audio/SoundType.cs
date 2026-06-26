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
        Timed,
        Background,
        Foreground,
        UI,
        Notification,
        CardEffect,
        CardAction,
    }

}
