#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    /// <summary>
    /// Models a single visual-novel scene definition parsed from a
    /// <c>scenes/*.json</c> file. Holds the scene's metadata (name,
    /// default backgrounds, ambient, etc.), a flat list of attached
    /// <see cref="ScenarioDefInfo"/> scripts, and any validation
    /// errors discovered by <see cref="Services.SceneValidator"/>.
    /// </summary>
    public sealed class SceneDefInfo
    {
        /// <summary>
        /// The logical name of the scene as declared in the JSON's
        /// <c>"name"</c> field. This is the key used for cross-scene
        /// references (e.g. <c>switch_scene</c> targets) and must be
        /// unique across all mods.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The mod that owns this scene definition (matches <see cref="ModSceneInfo.ModId"/>).
        /// </summary>
        public string ModId { get; set; }

        /// <summary>
        /// Path to the scene JSON file relative to the mod's root directory
        /// (e.g. <c>"scenes/main_scene.json"</c>).
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The raw JSON text of the scene definition file, preserved for
        /// display in the scenario text panel.
        /// </summary>
        public string RawJson { get; set; }

        /// <summary>
        /// Background IDs declared in the <c>"defaultBackground"</c> field.
        /// May contain one or more entries (the field accepts both a single
        /// string and a JSON array).
        /// </summary>
        public List<string> DefaultBackgrounds { get; set; } = new();

        /// <summary>
        /// The value of the <c>"defaultAmbient"</c> field, if any.
        /// </summary>
        public string DefaultAmbient { get; set; }

        /// <summary>
        /// The value of the <c>"defaultScenario"</c> field, nominating
        /// which scenario the engine should run when no higher-priority
        /// scenario matches.
        /// </summary>
        public string DefaultScenario { get; set; }

        /// <summary>
        /// All <c>*.scenario</c> scripts that declare this scene as their
        /// target (matched via the scenario's <c>scene:</c> meta field).
        /// </summary>
        public List<ScenarioDefInfo> Scenarios { get; set; } = new();

        /// <summary>
        /// Set to <c>true</c> by <see cref="Services.SceneValidator"/>
        /// when either the scene itself or any of its scenarios has
        /// validation errors.
        /// </summary>
        public bool HasErrors { get; set; }

        /// <summary>
        /// The aggregate list of validation messages for this scene,
        /// including errors from all attached scenarios.
        /// </summary>
        public List<ValidationMessage> Errors { get; set; } = new();
    }
}
#endif
