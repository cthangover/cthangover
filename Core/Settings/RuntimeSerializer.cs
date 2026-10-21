using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Pure marshalling helper: packs <see cref="RuntimeData"/> into a
    /// <c>"__core__"</c> <see cref="ModSlot"/> (named
    /// <see cref="DataBlob"/> blocks per subsystem) and restores it
    /// back. Does not touch filesystem, validation, or mod dispatch —
    /// that is the responsibility of <see cref="SaveService"/>.
    /// </summary>
    public static class RuntimeSerializer
    {
        /// <summary>
        /// Lightweight DTO serialised into
        /// <see cref="ModMetadata.CustomData"/> of the
        /// <c>"__core__"</c> slot so that
        /// <see cref="SaveService.GetSaveSlots"/> can extract preview
        /// info without deserialising full game-state blobs.
        /// </summary>
        public class CoreSlotPreview
        {
            /// <summary>Scene the player was in when the save was written.</summary>
            public string CurrentSceneName { get; set; }

            /// <summary>In-game clock tick at save moment.</summary>
            public long GameTime { get; set; }

            /// <summary>Number of recruited characters at save moment.</summary>
            public int CharacterCount { get; set; }
        }

        /// <summary>
        /// Builds the <c>"__core__"</c> <see cref="ModSlot"/> with
        /// named <see cref="DataBlob"/> blocks for each subsystem
        /// (Time, Lamp, ActionPool, Skills, CompletedScenarios, MapZones) and
        /// slot-level preview metadata in
        /// <see cref="ModMetadata.CustomData"/>. Subsystem-owned state is not
        /// part of the core slot: quest state persists through the quests
        /// engine mod's own <c>IModSaveable</c> slot, inventory and recipe
        /// state through the items engine mod's slot, and the character
        /// roster (<c>Characters</c>/<c>BattleSet</c>) through the
        /// characters engine mod's slot (<c>CharactersSaveable</c>).
        /// </summary>
        public static ModSlot BuildCoreSlot(
            RuntimeData runtime,
            string coreVersion,
            string currentSceneName,
            HashSet<string> completedScenarioIds)
        {
            var slot = new ModSlot
            {
                Metadata = new ModMetadata
                {
                    ModId          = SavePackage.CoreSlotKey,
                    ModName        = "Core Game",
                    MinCoreVersion = coreVersion,
                    IsRequired     = true,
                }
            };

            slot.Data["Time"] = new DataBlob(1, runtime.Time.Tick);

            var lampBlob = new DataBlob(1);
            lampBlob.SetFloat("Radius", runtime.LampData.Radius);
            lampBlob.SetFloat("Influence", runtime.LampData.Influence);
            slot.Data["Lamp"] = lampBlob;

            slot.Data["ActionPool"] = new DataBlob(1, runtime.ActionPool.ActionIds.ToList());
            slot.Data["Skills"]     = new DataBlob(1, runtime.SkillData.Skills.ToList());
            slot.Data["CompletedScenarios"] = new DataBlob(1, completedScenarioIds.ToList());
            slot.Data["MapZones"]   = new DataBlob(1, Cthangover.Core.Actions.MapStateBridge.ExportState());
            slot.Data["Flags"]      = new DataBlob(1, runtime.Flags);

            var preview = new CoreSlotPreview
            {
                CurrentSceneName = currentSceneName,
                GameTime         = runtime.Time.Tick,
                CharacterCount   = runtime.CharacterData.Characters?.Count ?? 0,
            };
            slot.Metadata.CustomData = JsonSerializer.Serialize(preview, JsonOptionsProvider.WriteCompactOptions);

            return slot;
        }

        /// <summary>
        /// Restores core state from the <c>"__core__"</c> slot's blobs
        /// back into the given <paramref name="runtime"/> instance.
        /// Silently skips any missing blob key — the corresponding
        /// subsystem keeps its previous value, except
        /// <see cref="RuntimeData.CompletedScenarioIds"/> which is
        /// always reset first: a loaded save is the authoritative
        /// completion history of its own playthrough, so completions
        /// from a previously running game must never leak into it.
        /// </summary>
        public static void RestoreCoreState(ModSlot coreSlot, RuntimeData runtime)
        {
            if (coreSlot.Data == null)
                return;

            runtime.CompletedScenarioIds.Clear();

            if (coreSlot.Data.TryGetValue("Time", out var timeBlob))
                runtime.Time.SetTime(timeBlob.ReadJson<long>());

            if (coreSlot.Data.TryGetValue("Lamp", out var lampBlob))
            {
                runtime.LampData.Radius    = lampBlob.GetFloat("Radius", 100f);
                runtime.LampData.Influence = lampBlob.GetFloat("Influence", 0.5f);
            }

            if (coreSlot.Data.TryGetValue("ActionPool", out var apBlob))
                runtime.ActionPool.ActionIds = apBlob.ReadJson<List<string>>() ?? new List<string>();

            if (coreSlot.Data.TryGetValue("Skills", out var skillsBlob))
            {
                runtime.SkillData.Skills.Clear();
                var skills = skillsBlob.ReadJson<List<string>>();
                if (skills != null)
                    foreach (var id in skills)
                        runtime.SkillData.AddSkill(id);
            }

            if (coreSlot.Data.TryGetValue("CompletedScenarios", out var csBlob))
            {
                runtime.LoadedCompletedScenarioIds = csBlob.ReadJson<List<string>>()?.ToHashSet()
                    ?? new HashSet<string>();
                runtime.CompletedScenarioIds.UnionWith(runtime.LoadedCompletedScenarioIds);
            }

            if (coreSlot.Data.TryGetValue("Flags", out var flagsBlob))
                runtime.Flags = flagsBlob.ReadJson<Dictionary<string, string>>() ?? new Dictionary<string, string>();

            if (coreSlot.Data.TryGetValue("MapZones", out var mapBlob))
                Cthangover.Core.Actions.MapStateBridge.ImportState(
                    mapBlob.ReadJson<Cthangover.Core.UI.Tool.MapZoneStateData>());
        }

        /// <summary>
        /// Reads slot preview metadata from
        /// <see cref="ModMetadata.CustomData"/> JSON. Returns defaults
        /// when the field is absent or malformed.
        /// </summary>
        public static CoreSlotPreview ReadSlotPreview(ModMetadata meta)
        {
            var result = new CoreSlotPreview();
            if (meta?.CustomData == null)
                return result;

            try
            {
                var preview = JsonSerializer.Deserialize<CoreSlotPreview>(meta.CustomData, JsonOptionsProvider.ReadOptions);
                if (preview != null)
                    return preview;
            }
            catch { }

            return result;
        }
    }
}
