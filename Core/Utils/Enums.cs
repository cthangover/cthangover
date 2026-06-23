using System;

namespace Cthangover.Core.Utils
{
    public static class Enums<T>
    {
        public static T Parse(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }
}
