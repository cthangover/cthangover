using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Godot;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Converts a typed event into the Dictionary GDScript subscribers of the
    /// <see cref="ModEventBus"/> bridge receive: one snake_case key per public
    /// property. <see cref="Variant.From{T}"/> dispatches on the static type,
    /// so a value held as <c>object</c> must be converted by its runtime type
    /// here — calling it with <c>object</c> throws for every value, which is
    /// how the bridge used to fail on every event with a payload. Enums become
    /// their names, collections Godot arrays, other objects nested
    /// dictionaries; null properties are left out.
    /// </summary>
    public static class ModEventPayload
    {
        /// <summary>The public instance properties of <paramref name="source"/> as a snake_case Dictionary.</summary>
        public static Godot.Collections.Dictionary ToDictionary(object source)
        {
            var data = new Godot.Collections.Dictionary();
            foreach (var prop in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                var value = prop.GetValue(source);
                if (value != null)
                    data[SnakeCase(prop.Name)] = ToVariant(value);
            }
            return data;
        }

        private static Variant ToVariant(object value)
        {
            switch (value)
            {
                case GodotObject godotObject: return godotObject;
                case string text: return text;
                case bool flag: return flag;
                case int number: return number;
                case long number: return number;
                case float number: return number;
                case double number: return number;
                case Enum enumValue: return enumValue.ToString();
                case IEnumerable items:
                    var array = new Godot.Collections.Array();
                    foreach (var item in items)
                        array.Add(item == null ? default : ToVariant(item));
                    return array;
                default: return ToDictionary(value);
            }
        }

        private static string SnakeCase(string pascal)
        {
            var result = new StringBuilder(pascal.Length + 4);
            for (var i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i]))
                    result.Append('_');
                result.Append(char.ToLowerInvariant(pascal[i]));
            }
            return result.ToString();
        }
    }
}
