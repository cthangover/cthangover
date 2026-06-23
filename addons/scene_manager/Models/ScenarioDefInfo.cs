#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    public sealed class ScenarioDefInfo
    {
        public string Name { get; set; }
        public string SceneName { get; set; }
        public int Priority { get; set; }
        public string Condition { get; set; }
        public string FilePath { get; set; }
        public string AbsoluteFilePath { get; set; }
        public string RawText { get; set; }
        public List<string> BackgroundRefs { get; set; } = new();
        public List<string> SwitchSceneTargets { get; set; } = new();
        public List<string> LocaleKeys { get; set; } = new();
        public List<string> AvatarKeys { get; set; } = new();
        public List<string> QuestRefs { get; set; } = new();
        public List<ValidationMessage> Errors { get; set; } = new();
    }
}
#endif
