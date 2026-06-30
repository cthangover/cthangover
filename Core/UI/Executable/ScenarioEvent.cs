using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// ExecutableEvent backed by a scenario file (.scenario). Converts scenario
    /// text using ScenarioConverter and appends the resulting actions to the
    /// dialog queue. Resolves the scenario path via convention: if ScenarioPath
    /// is set, uses that; otherwise derives the path from the class name
    /// (stripping "Event" suffix, mapping to res://mods/core/scenarios/).
    /// CustomMappings allows code to register arbitrary class-to-path bindings
    /// for mod-provided scenarios. Supports inline ScenarioText for embedded
    /// scenarios that don't need a separate file. Checks conditions through
    /// ScenarioCondition.Evaluate before triggering.
    /// </summary>
    public partial class ScenarioEvent : ExecutableEvent
    {
        private static readonly Dictionary<string, string> CustomMappings = new();

        /// <summary>
        /// Registers a custom mapping from a class name to a scenario file path.
        /// Use from mod initialization code for mod-provided scenario paths that
        /// deviate from the default naming convention.
        /// </summary>
        /// <param name="className">The C# class name (without namespace).</param>
        /// <param name="scenarioPath">The resource path to the .scenario file.</param>
        public static void RegisterScenario(string className, string scenarioPath)
        {
            CustomMappings[className] = scenarioPath;
        }

        /// <summary>
        /// Explicit resource path to a scenario file. When set, overrides the
        /// default path resolution in <see cref="ResolveScenarioPath"/>.
        /// </summary>
        [Export] public string ScenarioPath { get; set; }

        /// <summary>
        /// Inline scenario text. If non-empty, parsed by <see cref="ScenarioConverter"/>
        /// instead of loading from a file. Enables embedding small scenarios directly
        /// in code or scene data.
        /// </summary>
        public string ScenarioText { get; set; }

        /// <summary>
        /// A condition expression evaluated by <see cref="ScenarioCondition.Evaluate"/>.
        /// If empty, the event always passes condition checks.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Stable identifier for save/load tracking. If set, becomes the value of
        /// <see cref="ExecutableEvent.ID"/> instead of the type name.
        /// </summary>
        public string ScenarioId { get; set; }

        /// <summary>
        /// Overrides the time-of-day lighting flag for this event's dialog.
        /// <c>true</c> enables time blending, <c>false</c> disables it,
        /// <c>null</c> leaves the current setting unchanged.
        /// </summary>
        public bool? LightUseTime { get; set; }

        /// <summary>
        /// Returns <see cref="ScenarioId"/> if set; otherwise falls back to the
        /// full type name from <see cref="ExecutableEvent.ID"/>.
        /// </summary>
        public override string ID => ScenarioId ?? base.ID;

        protected override void CreateDialog(DialogQueue dlg)
        {
            if (LightUseTime.HasValue)
                dlg.LightUseTime(LightUseTime.Value);

            var converter = new ScenarioConverter();
            DialogQueue loaded = null;

            if (!string.IsNullOrEmpty(ScenarioText))
            {
                loaded = converter.Convert(ScenarioText);
            }
            else
            {
                var path = ResolveScenarioPath();
                if (!string.IsNullOrEmpty(path))
                    loaded = converter.ConvertFile(path);
            }

            if (loaded == null)
            {
                GameLogger.Log("SCENE", $"Failed to load scenario for {GetType().Name}", LogLevel.Error);
                return;
            }

            foreach (var action in loaded.Queue)
                dlg.Queue.Add(action);

            foreach (var obj in loaded.RuntimeObjectList)
                dlg.RuntimeObjectList.Add(obj);
        }

        public override bool CheckConditions
        {
            get
            {
                if (!string.IsNullOrEmpty(Condition))
                    return ScenarioCondition.Evaluate(Condition);
                return true;
            }
        }

        private string ResolveScenarioPath()
        {
            if (!string.IsNullOrEmpty(ScenarioPath))
                return ScenarioPath;

            var typeName = GetType().FullName;
            var parts = typeName.Split('.');
            var className = parts[parts.Length - 1];

            if (CustomMappings.TryGetValue(className, out var mapped))
                return mapped;

            if (className.EndsWith("Event"))
                className = className.Substring(0, className.Length - "Event".Length);

            return $"res://mods/core/scenarios/{className}.scenario";
        }
    }
}
