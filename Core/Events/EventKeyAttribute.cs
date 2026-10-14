using System;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Строковый ключ для авто-моста тип → строка. При Publish&lt;T&gt;
    /// шина дополнительно бродкастит на этот ключ для GDScript-подписчиков.
    /// Без атрибута — fallback на typeof(T).Name в snake_case.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EventKeyAttribute : Attribute
    {
        /// <summary>The string event bus key the annotated class bridges to (snake_case, e.g. "scene_entered").</summary>
        public string Key { get; }
        /// <summary>Binds the annotated event class to <paramref name="key"/> on the string (GDScript) bus layer.</summary>
        public EventKeyAttribute(string key) => Key = key;
    }
}
