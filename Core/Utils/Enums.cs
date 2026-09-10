using System;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Generic enum parser that wraps <see cref="Enum.Parse(Type, string, bool)"/> with
    /// case-insensitive matching and a natural type-parameter syntax. Used across the
    /// project wherever config strings, serialized fields, or console commands must be
    /// converted into strongly-typed enum values without per-enum boilerplate.
    /// </summary>
    /// <typeparam name="T">The enum type to parse into.</typeparam>
    public static class Enums<T>
    {
        /// <summary>
        /// Parses the case-insensitive string <paramref name="value"/> into the enum
        /// type <typeparamref name="T"/>. Throws <see cref="ArgumentException"/> if
        /// the string does not match any named constant.
        /// </summary>
        public static T Parse(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }
}
