using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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

        internal ModAssertException(string message, Exception cause) : base(message, cause) { }
    }

    /// <summary>
    /// Minimal assertion API for mod unit tests. Every failure throws
    /// <see cref="ModAssertException"/> with a message that already names the
    /// expectation, so a failing report line reads without opening the source
    /// (the "understandable errors" step of the debug ladder, §8.2).
    /// </summary>
    public static class ModAssert
    {
        /// <summary>Compares values using their C# equality contract.</summary>
        public static void Equal<T>(T expected, T actual, string what = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new ModAssertException(Msg(what, $"expected {Format(expected)}, actual {Format(actual)}"));
        }

        /// <summary>Rejects a value that compares equal to the unexpected value.</summary>
        public static void NotEqual<T>(T unexpected, T actual, string what = null)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
                throw new ModAssertException(Msg(what, $"expected a value different from {Format(unexpected)}, actual {Format(actual)}"));
        }

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

        /// <summary>Compares two floats with the default absolute tolerance of 0.0001.</summary>
        public static void Equal(float expected, float actual, string what = null) => Equal(expected, actual, 1e-4f, what);

        /// <summary>Asserts two floats are equal within <paramref name="epsilon"/>.</summary>
        public static void Equal(float expected, float actual, float epsilon, string what = null)
        {
            if (!expected.Equals(actual) && !(Math.Abs(expected - actual) <= epsilon))
                throw new ModAssertException(Msg(what, $"expected {expected.ToString(CultureInfo.InvariantCulture)}, actual {actual.ToString(CultureInfo.InvariantCulture)} (±{epsilon})"));
        }

        /// <summary>Compares two doubles with an explicit absolute tolerance.</summary>
        public static void Equal(double expected, double actual, double epsilon, string what = null)
        {
            if (!expected.Equals(actual) && !(Math.Abs(expected - actual) <= epsilon))
                throw new ModAssertException(Msg(what, $"expected {Format(expected)} ±{Format(epsilon)}, actual {Format(actual)}"));
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

        /// <summary>Compares object identity even for types that override value equality.</summary>
        public static void Same(object expected, object actual, string what = null)
        {
            if (!ReferenceEquals(expected, actual))
                throw new ModAssertException(Msg(what, $"expected the same instance as {Format(expected)}, actual {Format(actual)}"));
        }

        /// <summary>Requires two distinct references.</summary>
        public static void NotSame(object unexpected, object actual, string what = null)
        {
            if (ReferenceEquals(unexpected, actual))
                throw new ModAssertException(Msg(what, $"expected a distinct instance, both were {Format(actual)}"));
        }

        /// <summary>Returns the value when its runtime type exactly matches <typeparamref name="T"/>.</summary>
        public static T IsType<T>(object value, string what = null)
        {
            if (value?.GetType() == typeof(T))
                return (T)value;
            throw new ModAssertException(Msg(what, $"expected exact type {typeof(T).Name}, actual {TypeName(value)}"));
        }

        /// <summary>Returns the value as the requested base type or interface.</summary>
        public static T IsAssignableFrom<T>(object value, string what = null)
        {
            if (value is T typed)
                return typed;
            throw new ModAssertException(Msg(what, $"expected a value assignable to {typeof(T).Name}, actual {TypeName(value)}"));
        }

        /// <summary>Compares elements in order and reports the first differing or missing index.</summary>
        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what = null,
            IEqualityComparer<T> comparer = null)
        {
            if (ReferenceEquals(expected, actual))
                return;
            if (expected == null || actual == null)
                throw new ModAssertException(Msg(what, $"expected {(expected == null ? "null" : "a sequence")}, actual {(actual == null ? "null" : "a sequence")}"));

            comparer ??= EqualityComparer<T>.Default;
            using var left = expected.GetEnumerator();
            using var right = actual.GetEnumerator();
            var index = 0;
            while (left.MoveNext())
            {
                if (!right.MoveNext())
                    throw new ModAssertException(Msg(what, $"sequence ended at index {index}, expected {Format(left.Current)}"));
                if (!comparer.Equals(left.Current, right.Current))
                    throw new ModAssertException(Msg(what, $"index {index}: expected {Format(left.Current)}, actual {Format(right.Current)}"));
                index++;
            }

            if (right.MoveNext())
                throw new ModAssertException(Msg(what, $"unexpected element at index {index}: {Format(right.Current)}"));
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

        /// <summary>Returns the expected exception or a subtype so its details can be checked.</summary>
        public static TException Throws<TException>(Action action, string what = null) where TException : Exception
        {
            ArgumentNullException.ThrowIfNull(action);
            try { action(); }
            catch (TException error) { return error; }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw ExceptionFailure(typeof(TException).Name, error, what);
            }
            throw ExceptionFailure(typeof(TException).Name, null, what);
        }

        /// <summary>Awaits the operation in the calling context and returns the expected exception or a subtype.</summary>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string what = null) where TException : Exception
        {
            ArgumentNullException.ThrowIfNull(action);
            try { await action(); }
            catch (TException error) { return error; }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw ExceptionFailure(typeof(TException).Name, error, what);
            }
            throw ExceptionFailure(typeof(TException).Name, null, what);
        }

        /// <summary>Adds scenario context to an unexpected synchronous failure.</summary>
        public static void DoesNotThrow(Action action, string what = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            try { action(); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw ExceptionFailure("no exception", error, what);
            }
        }

        /// <summary>Adds scenario context to an unexpected asynchronous failure.</summary>
        public static async Task DoesNotThrowAsync(Func<Task> action, string what = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            try { await action(); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw ExceptionFailure("no exception", error, what);
            }
        }

        /// <summary>Runs independent assertions in order and reports their failures together.</summary>
        public static void All(params Action[] assertions) => All(null, assertions);

        /// <summary>Runs independent assertions under a shared description, propagating other exceptions.</summary>
        public static void All(string what, params Action[] assertions)
        {
            ArgumentNullException.ThrowIfNull(assertions);
            var failures = new List<string>();
            foreach (var assertion in assertions)
            {
                try { assertion(); }
                catch (ModAssertException error) { failures.Add(error.Message); }
            }

            if (failures.Count != 0)
                throw new ModAssertException(Msg(what, $"{failures.Count} assertions failed\n" + string.Join("\n", failures)));
        }

        /// <summary>Fails explicitly when the scenario reaches an unexpected state.</summary>
        public static void Fail(string what) => throw new ModAssertException(Msg(what, "explicit failure"));

        private static ModAssertException ExceptionFailure(string expected, Exception actual, string what)
        {
            var detail = actual == null ? "no exception" : $"{TypeName(actual)} ({actual.Message})";
            return new ModAssertException(Msg(what, $"expected {expected}, actual {detail}"), actual);
        }

        private static string TypeName(object value) => value?.GetType().Name ?? "null";

        private static string Format<T>(T value) => value switch
        {
            null => "null",
            bool flag => flag ? "true" : "false",
            string text => $"'{text}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        private static string Msg(string what, string detail)
        {
            return string.IsNullOrEmpty(what)
                ? $"ModAssert failed: {detail}"
                : $"ModAssert failed [{what}]: {detail}";
        }
    }
}
