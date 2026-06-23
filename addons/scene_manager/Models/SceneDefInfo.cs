#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    public sealed class SceneDefInfo
    {
        public string Name { get; set; }
        public string ModId { get; set; }
        public string FilePath { get; set; }
        public string RawJson { get; set; }
        public List<string> DefaultBackgrounds { get; set; } = new();
        public string DefaultAmbient { get; set; }
        public string DefaultScenario { get; set; }
        public List<ScenarioDefInfo> Scenarios { get; set; } = new();
        public bool HasErrors { get; set; }
        public List<ValidationMessage> Errors { get; set; } = new();
    }
}
#endif
