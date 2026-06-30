#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    /// <summary>
    /// Represents a single parsed <c>*.scenario</c> script file.
    /// <see cref="Services.ScenarioParser"/> extracts the YAML-style
    /// metadata header (<c>scene</c>, <c>priority</c>, <c>condition</c>)
    /// and then scans the body for command lines that reference
    /// backgrounds, other scenes, locale keys, avatar aliases, and
    /// quest IDs. Those extracted references populate the various
    /// <c>*Refs</c> lists and are later validated by
    /// <see cref="Services.SceneValidator"/>.
    /// </summary>
    public sealed class ScenarioDefInfo
    {
        /// <summary>
        /// The scenario name derived from the file name without its
        /// extension (e.g. <c>"greeting"</c> from <c>"greeting.scenario"</c>).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The target scene name declared in the YAML header as <c>scene:</c>.
        /// This links the scenario to a <see cref="SceneDefInfo"/> whose
        /// <see cref="SceneDefInfo.Name"/> matches.
        /// </summary>
        public string SceneName { get; set; }

        /// <summary>
        /// Priority value from the <c>priority:</c> meta field.
        /// When multiple scenarios target the same scene, the engine
        /// evaluates them in ascending priority order and picks the
        /// first whose <see cref="Condition"/> passes.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// A C#-style boolean expression from the <c>condition:</c>
        /// meta field that determines whether this scenario should
        /// activate over other candidates for the same scene.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Path to the <c>.scenario</c> file relative to the owning
        /// mod's root directory (e.g. <c>"scenarios/greeting.scenario"</c>).
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Absolute filesystem path to the <c>.scenario</c> file,
        /// used by the "Open file" button in
        /// <see cref="Views.ScenarioTextPanel"/> to launch the
        /// system default editor.
        /// </summary>
        public string AbsoluteFilePath { get; set; }

        /// <summary>
        /// The complete raw text of the scenario file, shown verbatim
        /// in the text panel when the scenario is selected.
        /// </summary>
        public string RawText { get; set; }

        /// <summary>
        /// Background IDs referenced via <c>background</c> commands
        /// in the scenario body. Each ID is checked against the
        /// catalog of actual background files discovered by
        /// <see cref="Services.ResourceResolver"/>.
        /// </summary>
        public List<string> BackgroundRefs { get; set; } = new();

        /// <summary>
        /// Scene names targeted by <c>switch_scene</c> commands.
        /// Validated against the registered scene name set to catch
        /// typos and forward-references to non-existent scenes.
        /// </summary>
        public List<string> SwitchSceneTargets { get; set; } = new();

        /// <summary>
        /// Locale keys collected from all <c>key=</c> named parameters
        /// in the scenario body (e.g. dialog lines that display
        /// localized strings).
        /// </summary>
        public List<string> LocaleKeys { get; set; } = new();

        /// <summary>
        /// Avatar/sprite aliases extracted from <c>first=</c> and
        /// <c>second=</c> named parameters (the two-character dialog
        /// system uses these to determine which portraits to display).
        /// </summary>
        public List<string> AvatarKeys { get; set; } = new();

        /// <summary>
        /// Quest IDs pulled from <c>quest_id=</c> named parameters,
        /// validated against the quest registry parsed from all
        /// <c>quests/*.json</c> files across all mods.
        /// </summary>
        public List<string> QuestRefs { get; set; } = new();

        /// <summary>
        /// Validation errors specific to this scenario, appended by
        /// <see cref="Services.SceneValidator"/> during the per-scenario
        /// reference-checking pass.
        /// </summary>
        public List<ValidationMessage> Errors { get; set; } = new();
    }
}
#endif
