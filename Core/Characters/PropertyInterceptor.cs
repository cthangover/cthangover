using System;
using System.Globalization;
using Cthangover.Core.Utils.Types;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Typed base for <see cref="IPropertyInterceptor"/> implementations.
    /// Eliminates manual string parsing/formatting — the interceptor
    /// author overrides <see cref="Intercept(T, T)"/> working with typed
    /// values, and the base class handles conversion through
    /// <see cref="IParse{T}"/> (resolved via <see cref="TParser"/>).
    /// </summary>
    /// <typeparam name="T">
    /// The value type this interceptor operates on. Must match a parser
    /// registered in <see cref="TParser"/> (e.g. <c>int</c>, <c>int?</c>,
    /// <c>float</c>, <c>bool</c>, <c>string</c>).
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <c>prevValue</c> parses to <c>default(T)</c> when the property
    /// didn't exist (null for nullable types, 0/0f/false for primitives).
    /// The converted result uses <see cref="Convert.ToString(object, IFormatProvider)"/>
    /// with <see cref="CultureInfo.InvariantCulture"/> to ensure
    /// round-trip compatibility with <see cref="FloatParse"/>.
    /// </para>
    /// <para>
    /// Example — double healing:
    /// <code>
    /// class DoubleHeal : PropertyInterceptor&lt;int&gt;
    /// {
    ///     public override string PropertyName => "Health";
    ///     protected override int Intercept(int prev, int next)
    ///         => next > prev ? prev + (next - prev) * 2 : next;
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public abstract class PropertyInterceptor<T> : IPropertyInterceptor
    {
        private static readonly IParse<T> _parse = TParser.Resolve<T>();

        /// <inheritdoc cref="IPropertyInterceptor.Intercept"/>
        public string Intercept(string prevValue, string newValue)
        {
            var prev = _parse.Parse(prevValue);
            var next = _parse.Parse(newValue);
            var result = Intercept(prev, next);
            return Convert.ToString(result, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Type-safe interception point. Called when a property matching
        /// <see cref="PropertyName"/> is written. Receives the parsed
        /// previous and new values, returns the (possibly modified) new value.
        /// </summary>
        /// <param name="prevValue">
        /// Parsed previous value. For nullable types, <c>null</c> means
        /// the property didn't exist. For primitive types, 0/false means
        /// either absent or genuinely zero.
        /// </param>
        /// <param name="newValue">
        /// Parsed new value being written, possibly modified by earlier
        /// interceptors in the chain for the same property.
        /// </param>
        protected abstract T Intercept(T prevValue, T newValue);

        /// <inheritdoc/>
        public abstract string PropertyName { get; }
    }
}
