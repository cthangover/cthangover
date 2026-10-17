namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Flushes System.Text.Json's reflection caches before a mod context is
    /// released. Serializing a mod type leaves its <c>JsonTypeInfo</c> in the
    /// caching context of every options instance that touched it and, worse,
    /// emitted property accessors in the process-wide
    /// <c>ReflectionEmitCachingMemberAccessor</c> — both pin the mod's
    /// <c>AssemblyLoadContext</c> (found with <c>dotnet-dump gcroot</c> on the
    /// first in-process game switch).
    ///
    /// The runtime ships the exact operation for .NET hot reload:
    /// <c>JsonSerializerOptionsUpdateHandler.ClearCache</c> (registered as a
    /// <c>MetadataUpdateHandler</c>) clears every tracked options instance and
    /// the emitted-accessor cache. It is internal, so it is invoked through
    /// reflection; if a future runtime renames it, the reset logs and the
    /// unload verification reports the leak instead of crashing.
    /// </summary>
    public static class JsonCacheReset
    {
        private static readonly MethodInfo ClearCacheMethod = typeof(JsonSerializerOptions).Assembly
            .GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler")
            ?.GetMethod("ClearCache", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>True when the runtime exposes the cache reset this class relies on.</summary>
        public static bool IsAvailable => ClearCacheMethod != null;

        /// <summary>Clears all System.Text.Json reflection caches. Returns false when the reset is unavailable or failed.</summary>
        public static bool Clear()
        {
            if (ClearCacheMethod == null)
                return false;
            try
            {
                ClearCacheMethod.Invoke(null, new object[] { null });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
