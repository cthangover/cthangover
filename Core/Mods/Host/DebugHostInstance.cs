namespace Cthangover.Core.Mods.Host
{
    using System;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Identity of one running game process for everything the debug host emits. The id is created once per
    /// process and survives host reinstalls, game switches and hot reloads, so a verifier can correlate log
    /// records, the discovery pointer and the handshake of exactly one instance. Two concrete checks become
    /// possible: a client that connected to the wrong process sees a mismatched id, and a discovery file
    /// overwritten by another instance no longer answers with the id it advertises.
    /// <para>
    /// Where the id surfaces: the full value appears once in the ready line, the short form prefixes every
    /// host log record (the log file is shared by concurrent instances, so interleaved lines stay
    /// attributable), the discovery document carries it as <c>instance</c>, and the <c>status</c> command
    /// repeats it on every handshake — the client-side comparison is what turns "debugging the wrong game"
    /// into a report instead of a mystery.
    /// </para>
    /// </summary>
    public static class DebugHostInstance
    {
        /// <summary>Process-lifetime identity; the same string appears in logs, discovery and status.</summary>
        public static Guid Id { get; } = Guid.NewGuid();

        /// <summary>Short form for dense log prefixes (first eight hex digits of the id).</summary>
        public static string ShortId
        {
            get
            {
                return Id.ToString("N")[..8];
            }
        }

        /// <summary>
        /// Writes one host log record attributed to this instance, so interleaved lines of concurrent
        /// instances stay distinguishable in the shared log file.
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Message)
        {
            GameLogger.Log("DEVHOST", "[" + ShortId + "] " + message, level);
        }
    }
}
