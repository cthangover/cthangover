namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Long-lived static slots of a "surviving" assembly, the kind of kernel
    /// state a mod fills from its initializer (bridges, lists, maps, a
    /// registry singleton) — the targets of the static reference sweep.
    /// </summary>
    public static class SweepHostSlots
    {
        public static Action Callback;
        public static object Slot;
        public static List<object> Items = new();
        public static Dictionary<string, object> ByName = new();
        public static HashSet<object> Set = new();
        public static readonly SweepHostRegistry Registry = new();

        public static void Reset()
        {
            Callback = null;
            Slot = null;
            Items.Clear();
            ByName.Clear();
            Set.Clear();
            Registry.Clear();
        }
    }
}
