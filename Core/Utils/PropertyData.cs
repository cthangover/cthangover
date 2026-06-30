using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cthangover.Core.Utils
{

    /// <summary>
    /// A string-keyed property bag with typed getters and setters, serving as
    /// the runtime representation of scenario entity state (characters, items,
    /// locations) and mod configuration data. Every value is stored as a plain
    /// <c>string</c> internally and parsed on demand by the typed accessors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The flat <c>string</c>-to-<c>string</c> dictionary allows the scenario
    /// scripting engine and mod JSON deserialisation to use a uniform property
    /// model without per-entity schema definitions. Type coercion happens at
    /// the getter/setter boundary, keeping the storage layer simple.
    /// </para>
    /// <para>
    /// Floating-point values use a culture-invariant <c>"."</c> decimal
    /// separator so that serialised JSON and scenario files produce identical
    /// results regardless of the host OS locale.
    /// </para>
    /// <para>
    /// Marked <see cref="SerializableAttribute"/> so that
    /// <see cref="System.Text.Json.JsonSerializer"/> can serialise
    /// <see cref="Values"/> directly.
    /// </para>
    /// </remarks>
    [Serializable]
    public class PropertyData
    {
        private static readonly NumberFormatInfo format;

        static PropertyData()
        {
            format = new NumberFormatInfo
            {
                NumberDecimalSeparator = "."
            };
        }

        /// <summary>
        /// The backing key-value store. Keys are property names; values are
        /// always strings. Exposed publicly so that serialisers and bulk-copy
        /// operations can iterate directly without going through individual
        /// getters.
        /// </summary>
        public IDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Retrieves the raw string stored under <paramref name="name"/>,
        /// or returns <paramref name="defaultValue"/> (which defaults to
        /// <c>null</c>) when the key is absent.
        /// </summary>
        public string GetStr(string name, string defaultValue = null)
        {
            if (!Values.TryGetValue(name, out var str))
                return defaultValue;
            return str;
        }

        /// <summary>
        /// Parses the stored string as a 32-bit signed integer.
        /// Returns <paramref name="defaultValue"/> (0) when the key is missing
        /// or the value is blank.
        /// </summary>
        public int GetInt(string name, int defaultValue = 0)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return int.Parse(str);
        }

        /// <summary>
        /// Parses the stored string as a single-precision float using the
        /// invariant <c>"."</c> decimal separator, bypassing OS locale settings.
        /// Returns <paramref name="defaultValue"/> (0f) on missing/blank keys.
        /// </summary>
        public float GetFloat(string name, float defaultValue = 0f)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return float.Parse(str, format);
        }

        /// <summary>
        /// Parses the stored string as a 64-bit signed integer.
        /// Returns <paramref name="defaultValue"/> (0) when the key is missing
        /// or the value is blank.
        /// </summary>
        public long GetLong(string name, long defaultValue = 0)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return long.Parse(str);
        }

        /// <summary>
        /// Parses the stored string as a boolean via <see cref="bool.Parse"/>.
        /// Always returns <c>false</c> when the key is missing or blank (there
        /// is no overload with a custom default because <c>false</c> is the
        /// conventional default for boolean properties).
        /// </summary>
        public bool GetBool(string name)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return false;
            return bool.Parse(str);
        }

        /// <summary>
        /// Stores a raw string value, overwriting any existing entry under
        /// the same <paramref name="name"/>.
        /// </summary>
        public void SetStr(string name, string value)
        {
            Values[name] = value;
        }

        /// <summary>
        /// Converts the integer to its default string representation and
        /// stores it under <paramref name="name"/>.
        /// </summary>
        public void SetInt(string name, int value)
        {
            Values[name] = value.ToString();
        }

        /// <summary>
        /// Converts the float to a culture-invariant string (dot decimal)
        /// and stores it under <paramref name="name"/>.
        /// </summary>
        public void SetFloat(string name, float value)
        {
            Values[name] = value.ToString(format);
        }

        /// <summary>
        /// Converts the 64-bit integer to its default string representation
        /// and stores it under <paramref name="name"/>.
        /// </summary>
        public void SetLong(string name, long value)
        {
            Values[name] = value.ToString();
        }

        /// <summary>
        /// Converts the boolean to <c>"True"</c> or <c>"False"</c> and stores
        /// it under <paramref name="name"/>.
        /// </summary>
        public void SetBool(string name, bool value)
        {
            Values[name] = value.ToString();
        }

        /// <summary>
        /// Returns <c>true</c> if a property with the given <paramref name="name"/>
        /// exists in the backing dictionary, regardless of its value.
        /// </summary>
        public bool Has(string name)
        {
            return Values.ContainsKey(name);
        }

        /// <summary>
        /// Removes the named property from the backing dictionary. No-op if
        /// the property does not exist.
        /// </summary>
        public void Remove(string name)
        {
            Values.Remove(name);
        }

        /// <summary>
        /// Creates a shallow copy where the <see cref="Values"/> dictionary
        /// contents are duplicated key-by-key into a new
        /// <see cref="Dictionary{String, String}"/>. The string values
        /// themselves are shared (immutable reference type), so this is
        /// effectively a deep-enough clone for property mutation scenarios.
        /// </summary>
        public PropertyData Clone()
        {
            var clone = new PropertyData();
            foreach (var kvp in Values)
                clone.Values[kvp.Key] = kvp.Value;
            return clone;
        }
    }

}
