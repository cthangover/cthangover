namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Defines severity tiers for log entries routed through <see cref="GameLogger"/>.
    /// Each level acts as a filter threshold: when <see cref="GameLogger"/> is configured
    /// with a <c>MinimumLevel</c>, any entry below that threshold is silently dropped
    /// before it reaches the console, Godot output, or file sinks. The numeric order
    /// is <c>Debug</c> &lt; <c>Message</c> &lt; <c>Warning</c> &lt; <c>Error</c>,
    /// enforced by <c>Enum</c> ordinal comparison inside the logger.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Verbose diagnostic output intended for development troubleshooting only.</summary>
        Debug,
        /// <summary>General informational entries suitable for production runtime.</summary>
        Message,
        /// <summary>Potentially harmful situations that do not interrupt execution.</summary>
        Warning,
        /// <summary>Recoverable or non-recoverable failures that are also written to a separate error log file.</summary>
        Error
    }
}
