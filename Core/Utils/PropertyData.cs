using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cthangover.Core.Utils
{

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

        public IDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        public string GetStr(string name, string defaultValue = null)
        {
            if (!Values.TryGetValue(name, out var str))
                return defaultValue;
            return str;
        }

        public int GetInt(string name, int defaultValue = 0)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return int.Parse(str);
        }

        public float GetFloat(string name, float defaultValue = 0f)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return float.Parse(str, format);
        }

        public long GetLong(string name, long defaultValue = 0)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return defaultValue;
            return long.Parse(str);
        }

        public bool GetBool(string name)
        {
            if (!Values.TryGetValue(name, out var str) || string.IsNullOrWhiteSpace(str))
                return false;
            return bool.Parse(str);
        }

        public void SetStr(string name, string value)
        {
            Values[name] = value;
        }

        public void SetInt(string name, int value)
        {
            Values[name] = value.ToString();
        }

        public void SetFloat(string name, float value)
        {
            Values[name] = value.ToString(format);
        }

        public void SetLong(string name, long value)
        {
            Values[name] = value.ToString();
        }

        public void SetBool(string name, bool value)
        {
            Values[name] = value.ToString();
        }

        public bool Has(string name)
        {
            return Values.ContainsKey(name);
        }

        public void Remove(string name)
        {
            Values.Remove(name);
        }

        public PropertyData Clone()
        {
            var clone = new PropertyData();
            foreach (var kvp in Values)
                clone.Values[kvp.Key] = kvp.Value;
            return clone;
        }
    }

}
