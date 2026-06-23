#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    public sealed class ModSceneInfo
    {
        public string ModId { get; set; }
        public string ModPath { get; set; }
        public List<SceneDefInfo> Scenes { get; set; } = new();
    }
}
#endif
