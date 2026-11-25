using System;
using System.Collections.Generic;

namespace Cthangover.Core.Settings
{
    /// <summary>Only an unambiguous rename may claim an ID absent from the current composition.</summary>
    internal static class SaveSlotAliases
    {
        internal static Dictionary<string, string> Build(IEnumerable<SaveGameMod> current)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SavePackage.CoreSlotKey };
            foreach (var mod in current)
            {
                if (string.IsNullOrWhiteSpace(mod?.Id))
                    continue;

                unavailable.Add(mod.Id);
                if (!string.IsNullOrWhiteSpace(mod.LegacySaveId) && !aliases.TryAdd(mod.LegacySaveId, mod.Id))
                    unavailable.Add(mod.LegacySaveId);
            }

            foreach (var id in unavailable)
                aliases.Remove(id);

            return aliases;
        }
    }
}
