using System;
using System.Diagnostics;
using System.Threading;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Immutable identity of the running case, published by the sequential mod runner. Reading it never
    /// discovers tests, changes clocks or retains a mod assembly.
    /// </summary>
    public sealed class ModTestProgress
    {
        private static ModTestProgress current;

        /// <summary>
        /// Atomically published immutable identity of the currently executing case, or null between cases;
        /// reading never retains a mod assembly or discovers tests.
        /// </summary>
        public static ModTestProgress Current
        {
            get
            {
                return Volatile.Read(ref current);
            }
        }

        /// <summary>
        /// Stable owning mod/suite identity copied by the sequential runner before entering the case.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// Exact discovered ModTest case identity, independent of localization and display captions.
        /// </summary>
        public string TestId { get; }

        /// <summary>
        /// One-based position of this case within the selected suite, validated against Total.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Number of selected discovered cases in this suite, used by the monitor without rediscovery.
        /// </summary>
        public int Total { get; }

        /// <summary>
        /// Completed suite wall seconds at case entry; finite and nonnegative, never driven by the gameplay
        /// clock.
        /// </summary>
        public double SuiteSecondsBeforeCase { get; }

        private readonly long started;

        /// <summary>
        /// Monotonic wall seconds since case publication, measured without modifying any simulation or test
        /// clock.
        /// </summary>
        public double CaseSeconds
        {
            get
            {
                return Stopwatch.GetElapsedTime(started).TotalSeconds;
            }
        }

        /// <summary>
        /// Completed suite wall time plus this case’s monotonic elapsed seconds for a live progress display.
        /// </summary>
        public double SuiteSeconds
        {
            get
            {
                return SuiteSecondsBeforeCase + CaseSeconds;
            }
        }

        /// <summary>
        /// Validates and freezes the selected case identity/counts and prior suite wall time; the runner
        /// publishes the instance separately.
        /// </summary>
        public ModTestProgress(string modId, string testId, int index, int total, double suiteSecondsBeforeCase)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(testId) || index < 1 || index > total
                || !double.IsFinite(suiteSecondsBeforeCase)
                || suiteSecondsBeforeCase < 0)
            {
                throw new ArgumentException("Invalid mod test progress");
            }

            ModId = modId;
            TestId = testId;
            Index = index;
            Total = total;
            SuiteSecondsBeforeCase = suiteSecondsBeforeCase;
            started = Stopwatch.GetTimestamp();
        }

        internal static void Publish(ModTestProgress value)
        {
            Volatile.Write(ref current, value);
        }
    }
}
