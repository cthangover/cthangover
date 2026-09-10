using System;
using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils.Types;

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
        private List<IPropertyInterceptor> _interceptors = new();
        private HashSet<string> _activeProperties = new();

        /// <summary>
        /// Registers an interceptor for a specific property key (defined
        /// by <see cref="IPropertyInterceptor.PropertyName"/>). Fires
        /// before every write to that property via any typed setter.
        /// </summary>
        public void AddInterceptor(IPropertyInterceptor interceptor) => _interceptors.Add(interceptor);
        /// <summary>
        /// Removes a previously registered interceptor. Safe to call
        /// during interception (though the current chain iteration
        /// completes before the list is updated).
        /// </summary>
        public void RemoveInterceptor(IPropertyInterceptor interceptor) => _interceptors.Remove(interceptor);
        /// <summary>
        /// Removes all registered interceptors at once. Typically used
        /// when unloading a mod that registered effect hooks.
        /// </summary>
        public void ClearInterceptors() => _interceptors.Clear();
        
        /// <summary>
        /// The backing key-value store. Keys are property names; values are
        /// always strings. Exposed publicly so that serialisers and bulk-copy
        /// operations can iterate directly without going through individual
        /// getters.
        /// </summary>
        public IDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Generic typed getter. Looks up the raw string in
        /// <see cref="Values"/>, parses it through the
        /// <see cref="IParse{T}"/> registered for
        /// <typeparamref name="T"/> in <see cref="TParser"/>, and
        /// returns the result. Falls back to
        /// <paramref name="defaultValue"/> when the key is absent
        /// or the parser returns <c>default(T)</c>.
        /// </summary>
        /// <typeparam name="T">Must have a parser registered in <see cref="TParser"/>.</typeparam>
        public T Get<T>(string name, T defaultValue)
        {
            if (!Values.TryGetValue(name, out var value))
                return defaultValue;
            return TParser.Resolve<T>().Parse(value) ?? defaultValue;
        }
        
        /// <summary>
        /// Generic typed setter. Formats <paramref name="value"/> through
        /// the <see cref="IParse{T}"/> registered for
        /// <typeparamref name="T"/>, then routes the resulting string
        /// through the interceptor chain — interceptors matching
        /// <paramref name="name"/> are called in registration order,
        /// each potentially modifying the value before it's stored in
        /// <see cref="Values"/>.
        /// </summary>
        /// <remarks>
        /// Uses a <c>_activeProperties</c> set to prevent infinite
        /// recursion: when interceptor A for "Defence" calls
        /// <c>SetInt("Health", ...)</c>, Health's interceptors fire
        /// normally, but writing back to "Defence" skips the chain.
        /// </remarks>
        /// <typeparam name="T">Must have a parser registered in <see cref="TParser"/>.</typeparam>
        public void Set<T>(string name, T value)
        {
            var parser = TParser.Resolve<T>();
            var text = parser.Format(value);
            if (!_activeProperties.Add(name))
            {
                Values[name] = text;
                return;
            }
            try
            {
                var prev = GetStr(name);
                foreach (var interceptor in _interceptors)
                {
                    if (interceptor.PropertyName == name)
                        text = interceptor.Intercept(prev, text);
                }
                Values[name] = text;
            }
            finally
            {
                _activeProperties.Remove(name);
            }
        }
        
        /// <summary>
        /// Retrieves the raw string stored under <paramref name="name"/>,
        /// or returns <paramref name="defaultValue"/> (which defaults to
        /// <c>null</c>) when the key is absent.
        /// </summary>
        public string GetStr(string name, string defaultValue = null)
        {
            return Get(name, defaultValue);
        }

        /// <summary>
        /// Parses the stored string as a 32-bit signed integer.
        /// Returns <paramref name="defaultValue"/> (0) when the key is missing
        /// or the value is blank.
        /// </summary>
        public int GetInt(string name, int defaultValue = 0)
        {
            return Get(name, defaultValue);
        }

        /// <summary>
        /// Parses the stored string as a single-precision float using the
        /// invariant <c>"."</c> decimal separator, bypassing OS locale settings.
        /// Returns <paramref name="defaultValue"/> (0f) on missing/blank keys.
        /// </summary>
        public float GetFloat(string name, float defaultValue = 0f)
        {
            return Get(name, defaultValue);
        }

        /// <summary>
        /// Parses the stored string as a 64-bit signed integer.
        /// Returns <paramref name="defaultValue"/> (0) when the key is missing
        /// or the value is blank.
        /// </summary>
        public long GetLong(string name, long defaultValue = 0)
        {
            return Get(name, defaultValue);
        }

        /// <summary>
        /// Parses the stored string as a boolean via <see cref="bool.Parse"/>.
        /// Always returns <c>false</c> when the key is missing or blank (there
        /// is no overload with a custom default because <c>false</c> is the
        /// conventional default for boolean properties).
        /// </summary>
        public bool GetBool(string name, bool defaultValue = false)
        {
            return Get(name, defaultValue);
        }

        /// <summary>
        /// Stores a raw string value, overwriting any existing entry under
        /// the same <paramref name="name"/>.
        /// </summary>
        public void SetStr(string name, string value)
        {
            Set(name, value);
        }

        /// <summary>
        /// Converts the integer to its default string representation and
        /// stores it under <paramref name="name"/>.
        /// </summary>
        public void SetInt(string name, int value)
        {
            Set(name, value);
        }

        /// <summary>
        /// Converts the float to a culture-invariant string (dot decimal)
        /// and stores it under <paramref name="name"/>.
        /// </summary>
        public void SetFloat(string name, float value)
        {
            Set(name, value);
        }

        /// <summary>
        /// Converts the 64-bit integer to its default string representation
        /// and stores it under <paramref name="name"/>.
        /// </summary>
        public void SetLong(string name, long value)
        {
            Set(name, value);
        }

        /// <summary>
        /// Converts the boolean to <c>"True"</c> or <c>"False"</c> and stores
        /// it under <paramref name="name"/>.
        /// </summary>
        public void SetBool(string name, bool value)
        {
            Set(name, value);
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

        /// <summary>
        /// Imports a single JSON key-value pair into the property bag.
        /// Dispatches by <c>JsonValueKind</c> to the appropriate typed
        /// setter. Arrays and objects are stored as raw JSON strings.
        /// No merge logic — just a plain copy.
        /// </summary>
        public static void ImportJsonProperty(PropertyData props, string key, object value)
        {
            if (value is System.Text.Json.JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Number:
                        if (je.TryGetInt32(out var i))
                            props.SetInt(key, i);
                        else
                            props.SetFloat(key, (float)je.GetDouble());
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        props.SetBool(key, je.GetBoolean());
                        break;
                    case System.Text.Json.JsonValueKind.String:
                        props.SetStr(key, je.GetString());
                        break;
                    default:
                        props.SetStr(key, je.GetRawText());
                        break;
                }
            }
            else
            {
                props.SetStr(key, value?.ToString());
            }
        }

        /// <summary>
        /// Imports all entries from a JSON extension-data dictionary into
        /// the property bag. No merge — each value is set directly.
        /// </summary>
        public static void ImportJsonProperties(PropertyData props, Dictionary<string, object> extraProperties)
        {
            if (extraProperties == null)
                return;

            foreach (var kvp in extraProperties)
                ImportJsonProperty(props, kvp.Key, kvp.Value);
        }
    }

}
