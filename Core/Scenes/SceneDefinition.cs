using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Defines a visual novel scene's default configuration as loaded from mod JSON
    /// manifests. Specifies fallback visuals (background images with optional randomization),
    /// ambient audio, and a default scenario script that executes when no other events
    /// are registered for the scene. Instantiated by <see cref="ModManager"/> during
    /// scene collection and consumed by <see cref="SceneManager.ApplySceneDefaults"/>.
    /// </summary>
    public class SceneDefinition
    {
        /// <summary>
        /// The unique scene identifier, matching scene names passed to
        /// <see cref="SceneManager.SwitchScene"/>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The identifier of the mod that defines this scene configuration.
        /// </summary>
        public string ModId { get; set; }

        /// <summary>
        /// One or more background texture IDs. When the scene is entered, a random entry
        /// is selected and loaded via <see cref="BackgroundFactory"/>. Supports both
        /// a single string value and a JSON array via <see cref="StringOrArrayConverter"/>.
        /// </summary>
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> DefaultBackground { get; set; }

        /// <summary>
        /// The ambient audio asset ID played when entering this scene. A null or empty
        /// value stops any currently playing ambient.
        /// </summary>
        public string DefaultAmbient { get; set; }

        /// <summary>
        /// The file path to a .scenario file that executes when no registered events
        /// exist for the scene. Acts as the visual novel fallback script.
        /// </summary>
        public string DefaultScenario { get; set; }
    }
}
