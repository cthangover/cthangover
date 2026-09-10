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
        public string Key { get; }
        public EventKeyAttribute(string key) => Key = key;
    }
}
