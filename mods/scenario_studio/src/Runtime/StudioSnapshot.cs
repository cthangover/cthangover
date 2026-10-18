namespace Cthangover.Mods.ScenarioStudio
{
    using System.Text.Json;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Game state at the moment a scenario started: a full save package
    /// (<see cref="SaveService.BuildPackage"/>) kept serialized in memory, plus
    /// the scenario and scene it belongs to. Restoring it is the "restart with
    /// rollback": quests, inventory, time and completion history go back to
    /// the scenario's start. It is kept as JSON because applying a package may
    /// reuse its slots, and one snapshot is restored again and again while a
    /// scenario is being polished.
    /// </summary>
    public sealed class StudioSnapshot
    {
        private readonly string _json;

        public string ScenarioName { get; }
        public string SceneName { get; }

        private StudioSnapshot(string scenarioName, string sceneName, string json)
        {
            ScenarioName = scenarioName;
            SceneName = sceneName;
            _json = json;
        }

        public static StudioSnapshot Capture(string scenarioName, string sceneName)
        {
            var package = SaveService.BuildPackage("studio_snapshot");
            return new StudioSnapshot(scenarioName, sceneName, JsonSerializer.Serialize(package, JsonOptionsProvider.WriteCompactOptions));
        }

        /// <summary>Brings the game state back; false when the package could not be applied.</summary>
        public bool Restore()
        {
            var package = JsonSerializer.Deserialize<SavePackage>(_json, JsonOptionsProvider.ReadOptions);
            return package != null && SaveService.ApplyPackage(package, "studio snapshot of " + ScenarioName);
        }
    }
}
