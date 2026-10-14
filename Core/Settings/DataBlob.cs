using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Smart container for a single logical entity (e.g. player stats,
    /// world map, textures). Each block carries its own schema version
    /// and a byte payload.
    ///
    /// Provides three data access modes:
    /// <list type="number">
    /// <item><b>Key-value API</b> (GetInt/SetInt etc.) — lazy access
    /// via an internal dictionary, serialised to JSON on flush.</item>
    /// <item><b>JSON API</b> (ReadJson/WriteJson) — direct
    /// serialisation/deserialisation of an object into the payload.</item>
    /// <item><b>Binary API</b> (GetBytes/SetBytes) — raw byte access
    /// for textures, compressed data, etc.</item>
    /// </list>
    ///
    /// Key-value and JSON methods conflict: calling <c>WriteJson</c>
    /// overwrites the internal dictionary. Calling <c>SetInt</c> after
    /// <c>WriteJson</c> replaces the JSON object with a dictionary.
    /// </summary>
    public class DataBlob
    {
        /// <summary>Schema version of the data in this block (default 1).</summary>
        public int Version { get; set; } = 1;

        private byte[] _payload = Array.Empty<byte>();

        /// <summary>
        /// Raw byte payload. Assigning <c>null</c> is coerced to an empty
        /// array. Unlike <see cref="GetBytes"/>, reading this property does
        /// <b>not</b> flush pending key-value entries into the bytes first.
        /// </summary>
        public byte[] Payload
        {
            get => _payload;
            set => _payload = value ?? Array.Empty<byte>();
        }

        // Lazy cache of _payload deserialised as Dictionary.
        // Private field, not persisted by System.Text.Json serialisation.
        private Dictionary<string, object> _dict;

        /// <summary>Empty blob with the default schema version 1.</summary>
        public DataBlob() { }

        /// <summary>Empty blob with an explicit schema version.</summary>
        public DataBlob(int version)
        {
            Version = version;
        }

        /// <summary>Creates a blob of the given version with <paramref name="data"/> serialized as the JSON payload.</summary>
        public DataBlob(int version, object data)
        {
            Version = version;
            WriteJson(data);
        }

        /// <summary>Creates a raw binary blob (version stays at the default 1).</summary>
        public DataBlob(byte[] data)
        {
            _payload = data ?? Array.Empty<byte>();
        }

        /// <summary>Reads an integer key, falling back to <paramref name="defaultValue"/> when missing or unparseable.</summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            EnsureDictLoaded();
            if (_dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    return je.GetInt32();
                try { return Convert.ToInt32(val); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        /// <summary>Reads a float key, falling back to <paramref name="defaultValue"/> when missing or unparseable.</summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            EnsureDictLoaded();
            if (_dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    return je.GetSingle();
                try { return Convert.ToSingle(val); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        /// <summary>Reads a string key, falling back to <paramref name="defaultValue"/> when missing.</summary>
        public string GetString(string key, string defaultValue = "")
        {
            EnsureDictLoaded();
            if (_dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement je && je.ValueKind == JsonValueKind.String)
                    return je.GetString() ?? defaultValue;
                return val?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        /// <summary>Reads a bool key, falling back to <paramref name="defaultValue"/> when missing or unparseable.</summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            EnsureDictLoaded();
            if (_dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement je && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False))
                    return je.GetBoolean();
                try { return Convert.ToBoolean(val); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        /// <summary>Writes an integer key (lazily flushed to the payload on <see cref="GetBytes"/>).</summary>
        public void SetInt(string key, int value)
        {
            EnsureDictLoaded();
            _dict[key] = value;
        }

        /// <summary>Writes a float key (lazily flushed to the payload on <see cref="GetBytes"/>).</summary>
        public void SetFloat(string key, float value)
        {
            EnsureDictLoaded();
            _dict[key] = value;
        }

        /// <summary>Writes a string key; null is stored as an empty string (lazily flushed on <see cref="GetBytes"/>).</summary>
        public void SetString(string key, string value)
        {
            EnsureDictLoaded();
            _dict[key] = value ?? "";
        }

        /// <summary>Writes a bool key (lazily flushed to the payload on <see cref="GetBytes"/>).</summary>
        public void SetBool(string key, bool value)
        {
            EnsureDictLoaded();
            _dict[key] = value;
        }

        /// <summary>
        /// Deserialises the payload as the given type.
        /// Returns <c>new T()</c> when the payload is empty or invalid.
        /// </summary>
        public T ReadJson<T>() where T : new()
        {
            if (_payload == null || _payload.Length == 0)
                return new T();

            try
            {
                var result = JsonSerializer.Deserialize<T>(_payload, JsonOptionsProvider.ReadOptions);
                return result ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        /// <summary>
        /// Serialises an object into the payload, overwriting the
        /// internal dictionary if one exists.
        /// </summary>
        public void WriteJson<T>(T data)
        {
            if (data == null)
            {
                _payload = Array.Empty<byte>();
                _dict = null;
                return;
            }

            _payload = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptionsProvider.WriteCompactOptions);
            _dict = null;
        }

        /// <summary>
        /// Returns the raw byte payload. If data was written via the
        /// key-value API, flushes the dictionary to JSON first.
        /// </summary>
        public byte[] GetBytes()
        {
            EnsurePayloadFromDict();
            return _payload;
        }

        /// <summary>Replaces the payload with raw bytes and discards any pending key-value dictionary.</summary>
        public void SetBytes(byte[] data)
        {
            _payload = data ?? Array.Empty<byte>();
            _dict = null;
        }

        private void EnsureDictLoaded()
        {
            if (_dict != null)
                return;

            _dict = new Dictionary<string, object>();

            if (_payload == null || _payload.Length == 0)
                return;

            try
            {
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(_payload, JsonOptionsProvider.ReadOptions);
                if (deserialized != null)
                    _dict = deserialized;
            }
            catch
            {
                _dict = new Dictionary<string, object>();
            }
        }

        private void EnsurePayloadFromDict()
        {
            if (_dict != null)
            {
                _payload = JsonSerializer.SerializeToUtf8Bytes(_dict, JsonOptionsProvider.WriteCompactOptions);
            }
        }
    }
}
