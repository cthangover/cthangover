using System;
using System.Globalization;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Assertion failure of a mod unit test. The runner reports PASS/FAIL
    /// purely by catching this exception (plus any other exception, which is
    /// reported as Failed with the original message), so no external test
    /// framework is pulled into the game runtime.
    /// </summary>
    public class ModAssertException : Exception
    {
        /// <summary>Creates a failure with a human-readable description.</summary>
        public ModAssertException(string message) : base(message) { }
    }

    /// <summary>
    /// Minimal assertion API for mod unit tests. Every failure throws
    /// <see cref="ModAssertException"/> with a message that already names the
    /// expectation, so a failing report line reads without opening the source
    /// (the "understandable errors" step of the debug ladder, §8.2).
    /// </summary>
    public static class ModAssert
    {
        /// <summary>Asserts two strings are equal.</summary>
        public static void Equal(string expected, string actual, string what = null)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new ModAssertException(Msg(what, $"expected '{expected}', actual '{actual}'"));
        }

        /// <summary>Asserts two ints are equal.</summary>
        public static void Equal(long expected, long actual, string what = null)
        {
            if (expected != actual)
                throw new ModAssertException(Msg(what, $"expected {expected}, actual {actual}"));
        }

        /// <summary>Asserts two floats are equal within <paramref name="epsilon"/>.</summary>
        public static void Equal(float expected, float actual, float epsilon = 1e-4f, string what = null)
        {
            if (Math.Abs(expected - actual) > epsilon)
                throw new ModAssertException(Msg(what, $"expected {expected.ToString(CultureInfo.InvariantCulture)}, actual {actual.ToString(CultureInfo.InvariantCulture)} (±{epsilon})"));
        }

        /// <summary>Asserts two doubles are equal within <paramref name="epsilon"/> (CloseTo).</summary>
        public static void CloseTo(double expected, double actual, double epsilon = 1e-4, string what = null)
        {
            if (double.IsNaN(expected) || double.IsNaN(actual) || Math.Abs(expected - actual) > epsilon)
                throw new ModAssertException(Msg(what, $"expected {expected.ToString(CultureInfo.InvariantCulture)} ±{epsilon}, actual {actual.ToString(CultureInfo.InvariantCulture)}"));
        }

        /// <summary>Asserts the condition is true.</summary>
        public static void True(bool condition, string what = null)
        {
            if (!condition)
                throw new ModAssertException(Msg(what, "expected true"));
        }

        /// <summary>Asserts the condition is false.</summary>
        public static void False(bool condition, string what = null)
        {
            if (condition)
                throw new ModAssertException(Msg(what, "expected false"));
        }

        /// <summary>Asserts the value is null.</summary>
        public static void Null(object value, string what = null)
        {
            if (value != null)
                throw new ModAssertException(Msg(what, $"expected null, actual '{value}'"));
        }

        /// <summary>Asserts the value is not null.</summary>
        public static void NotNull(object value, string what = null)
        {
            if (value == null)
                throw new ModAssertException(Msg(what ?? "value", "is null"));
        }

        /// <summary>
        /// Asserts the action throws <see cref="ModAssertException"/> (or any
        /// exception when <paramref name="anyException"/>). Fails when nothing
        /// is thrown.
        /// </summary>
        public static void Throws(Action action, string what = null, bool anyException = false)
        {
            try
            {
                action();
            }
            catch (ModAssertException)
            {
                return;
            }
            catch (Exception ex) when (anyException)
            {
                _ = ex;
                return;
            }
            throw new ModAssertException(Msg(what, "expected an exception, but none was thrown"));
        }

        private static string Msg(string what, string detail)
        {
            return string.IsNullOrEmpty(what)
                ? $"ModAssert failed: {detail}"
                : $"ModAssert failed [{what}]: {detail}";
        }
    }
}
