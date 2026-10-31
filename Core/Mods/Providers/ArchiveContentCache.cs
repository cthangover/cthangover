namespace Cthangover.Core.Mods.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>A process-wide plaintext budget shared by all mounted archives, released per provider on unload.</summary>
    internal static class ArchiveContentCache
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private const int MaximumFileBytes = 4 * 1024 * 1024;
        private static readonly object gate = new();
        private static readonly Dictionary<(object Owner, string Path), byte[]> values = new();
        private static readonly Queue<(object Owner, string Path)> order = new();
        private static int bytes;

        internal static Stream Open(object owner, string path)
        {
            lock (gate)
                return values.TryGetValue((owner, path), out var data) ? new MemoryStream(data, false) : null;
        }

        internal static void Remember(object owner, string path, byte[] data)
        {
            if (data.Length > MaximumFileBytes)
                return;
            lock (gate)
            {
                var key = (owner, path);
                if (values.ContainsKey(key))
                    return;
                while (bytes + data.Length > MaximumBytes && order.Count > 0)
                {
                    var oldest = order.Dequeue();
                    bytes -= values[oldest].Length;
                    values.Remove(oldest);
                }
                values[key] = (byte[])data.Clone();
                order.Enqueue(key);
                bytes += data.Length;
            }
        }

        internal static void Release(object owner)
        {
            lock (gate)
            {
                var count = order.Count;
                for (var index = 0; index < count; index++)
                {
                    var key = order.Dequeue();
                    if (ReferenceEquals(key.Owner, owner))
                    {
                        bytes -= values[key].Length;
                        values.Remove(key);
                    }
                    else
                        order.Enqueue(key);
                }
            }
        }
    }
}
