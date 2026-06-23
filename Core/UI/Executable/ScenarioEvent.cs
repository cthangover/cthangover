using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Executable
{
    public partial class ScenarioEvent : ExecutableEvent
    {
        private static readonly Dictionary<string, string> CustomMappings = new();

        public static void RegisterScenario(string className, string scenarioPath)
        {
            CustomMappings[className] = scenarioPath;
        }

        [Export] public string ScenarioPath { get; set; }

        public string ScenarioText { get; set; }

        public string Condition { get; set; }

        public string ScenarioId { get; set; }

        public bool? LightUseTime { get; set; }

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
