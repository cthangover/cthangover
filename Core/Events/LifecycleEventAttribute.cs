using System;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Маркер: событие поддерживает приоритеты подписчиков.
    /// Меньшее значение priority = вызывается раньше.
    /// Применять только к lifecycle-событиям (Scene/Battle).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LifecycleEventAttribute : Attribute { }
}
