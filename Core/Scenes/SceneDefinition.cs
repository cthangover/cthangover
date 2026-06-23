using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    public class SceneDefinition
    {
        public string Name { get; set; }
        public string ModId { get; set; }

        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> DefaultBackground { get; set; }

        public string DefaultAmbient { get; set; }
        public string DefaultScenario { get; set; }
    }
}
