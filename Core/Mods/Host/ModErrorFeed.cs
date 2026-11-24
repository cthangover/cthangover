namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Bounded structured tail of warning and error log records for tooling. The feed subscribes to
    /// <see cref="GameLogger.LogEmitted"/> before boot compiles mods, keeps the newest records
    /// under a monotonic sequence, and serves pulls above a caller-supplied cursor, so an agent reads
    /// failures as data instead of parsing log files. Warnings and errors only: informational flow never
    /// reaches the buffer. In the packet model this is the backing store of the host's error topic.
    /// </summary>
    public static class ModErrorFeed
    {
        private const int Capacity = 512;
        private const int MaxMessageLength = 4000;
        private static readonly object Lock = new();
        private static readonly List<Record> Records = new(Capacity);
        private static long sequence;
        private static bool initialized;

        /// <summary>One buffered log record; fields are detached copies of the log call.</summary>
        public sealed class Record
        {
            /// <summary>Monotonic record number, stable across pulls.</summary>
            public long Sequence { get; }

            /// <summary>UTC timestamp of the log call.</summary>
            public DateTimeOffset Time { get; }

            /// <summary>Logger category, e.g. MODS_COMPILE or ACTOR3D.</summary>
            public string Category { get; }

            /// <summary>Severity; the feed only stores Warning and above.</summary>
            public LogLevel Level { get; }

            /// <summary>Message text, truncated at a tooling-friendly length.</summary>
            public string Message { get; }

            /// <summary>Provider that produced a structured compiler diagnostic.</summary>
            public string Mod { get; }

            /// <summary>Compiler diagnostic identifier, such as CS0103.</summary>
            public string Code { get; }

            /// <summary>Source location when the compiler associates the record with a file.</summary>
            public string File { get; }

            /// <summary>One-based source line, when available.</summary>
            public int? Line { get; }

            /// <summary>One-based source column, when available.</summary>
            public int? Column { get; }

            internal Record(long sequenceNumber, DateTimeOffset time, string category, LogLevel level, string message,
                string mod, string code, string file, int? line, int? column)
            {
                Sequence = sequenceNumber;
                Time = time;
                Category = category;
                Level = level;
                Message = message;
                Mod = mod;
                Code = code;
                File = file;
                Line = line;
                Column = column;
            }
        }

        /// <summary>Latest sequence handed out; zero until the first record arrives.</summary>
        public static long Latest
        {
            get
            {
                lock (Lock)
                {
                    return Records.Count > 0 ? Records[^1].Sequence : 0;
                }
            }
        }

        /// <summary>
        /// Subscribes once before compilation, so a client connecting after boot can still diagnose startup.
        /// </summary>
        public static void Initialize()
        {
            lock (Lock)
            {
                if (initialized)
                {
                    return;
                }

                initialized = true;
                GameLogger.LogEmitted += OnEmitted;
            }
        }

        /// <summary>
        /// Captures compiler details independently of the configured log threshold. The compiler's
        /// error summary and its per-source diagnostics have different log levels.
        /// </summary>
        internal static void RecordCompilation(string mod, string message, string code = null, string file = null,
            int? line = null, int? column = null)
        {
            Add("MODS_COMPILE", message, LogLevel.Error, mod, code, file, line, column);
        }

        /// <summary>Captures records and their paging cursor together, before another producer can append.</summary>
        internal static JsonObject Read(long? since, int limit, LogLevel minimum, string mod)
        {
            List<Record> page;
            long latest;
            long next;
            bool hasMore;
            bool olderAvailable;
            bool dropped;
            lock (Lock)
            {
                latest = sequence;
                var matches = new List<Record>();
                foreach (var record in Records)
                {
                    if (record.Sequence > (since ?? 0) && record.Level >= minimum && MatchesMod(record, mod))
                        matches.Add(record);
                }

                var start = since.HasValue ? 0 : Math.Max(0, matches.Count - limit);
                page = matches.GetRange(start, Math.Min(limit, matches.Count - start));
                hasMore = since.HasValue && matches.Count > page.Count;
                olderAvailable = start > 0;
                next = hasMore ? page[^1].Sequence : latest;
                dropped = since.HasValue && Records.Count > 0 && since.Value < Records[0].Sequence - 1;
            }

            return new JsonObject
            {
                ["entries"] = JsonSerializer.SerializeToNode(page),
                ["latest"] = latest,
                ["next"] = next,
                ["has_more"] = hasMore,
                ["older_available"] = olderAvailable,
                ["history_truncated"] = dropped
            };
        }

        private static bool MatchesMod(Record record, string mod)
        {
            if (mod == null)
                return true;
            if (record.Mod != null)
                return string.Equals(record.Mod, mod, StringComparison.OrdinalIgnoreCase);
            return record.Message.Contains(mod, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns warning-or-higher records above <paramref name="since"/> (the caller's last seen sequence),
        /// oldest first, capped to the newest <paramref name="maxCount"/> entries above the minimum
        /// <paramref name="level"/>. Records below the capacity watermark are already gone; a caller should
        /// advance its cursor and treat gaps as lost history.
        /// </summary>
        public static IReadOnlyList<Record> Pull(long since, int maxCount, LogLevel minimum)
        {
            lock (Lock)
            {
                var entries = new List<Record>();
                foreach (var record in Records)
                {
                    if (record.Sequence > since && record.Level >= minimum)
                    {
                        entries.Add(record);
                    }
                }

                return entries.Count > maxCount
                    ? entries.GetRange(entries.Count - maxCount, maxCount)
                    : entries;
            }
        }

        private static void OnEmitted(string category, string message, LogLevel level)
        {
            if (level < LogLevel.Warning)
            {
                return;
            }

            Add(category, message, level);
        }

        private static void Add(string category, string message, LogLevel level, string mod = null, string code = null,
            string file = null, int? line = null, int? column = null)
        {
            lock (Lock)
            {
                if (!initialized)
                    return;

                sequence++;
                Records.Add(new Record(sequence, DateTimeOffset.UtcNow, category, level,
                    message.Length > MaxMessageLength ? message[..MaxMessageLength] : message,
                    mod, code, file, line, column));
                if (Records.Count > Capacity)
                {
                    Records.RemoveRange(0, Records.Count - Capacity);
                }
            }
        }
    }
}
