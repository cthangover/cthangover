using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Relationship;
using Cthangover.Core.Scenes;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Central mutable state bag for the entire game session. Every subsystem
    /// — time, characters, inventory, recipes, lamp, recruiting, skills,
    /// and battle state — lives here. <see cref="SaveService.Save"/> samples
    /// all these fields into a <see cref="SaveData"/> DTO; <see cref="SaveService.Load"/>
    /// writes them back. The <see cref="CurrentScene"/> property resolves
    /// the active scene name into a <see cref="Cthangover.Core.Scenes.GodotSceneType"/>
    /// enum for conditional logic elsewhere.
    /// </summary>
    public class RuntimeData
    {
        /// <summary>In-game clock (minutes-based) driving day/night cycles.</summary>
        public TimeData Time { get; set; } = new TimeData(143546335);

        /// <summary>Active battle encounter data; <c>null</c> when not in battle.</summary>
        public BattleData     BattleData     { get; set; } = null;
        /// <summary>Recruited character roster with attributes and battle party set.</summary>
        public CharacterData  CharacterData  { get; set; } = new();
        /// <summary>Active recruiting entries with cooldown timers.</summary>
        public RecruitingData RecruitingData { get; set; } = new();
        /// <summary>Unlocked skills and their cooldown state.</summary>
        public SkillData      SkillData      { get; set; } = new();
        /// <summary>Player inventory of item stacks.</summary>
        public Inventory      Inventory      { get; set; } = new();
        /// <summary>Unlocked recipe IDs and lookup helpers.</summary>
        public RecipeData     RecipeData     { get; set; } = new();
        /// <summary>Lamp light radius and influence state.</summary>
        public LampData       LampData       { get; set; } = new();
        /// <summary>Persistent collection of action IDs available for assignment via the character panel.</summary>
        public ActionPoolData ActionPool     { get; set; } = new();

        /// <summary>
        /// IDs of one-shot scenarios already completed in this session.
        /// Populated by <see cref="SaveService.Load"/> and consumed by
        /// the scenario runner to skip re-triggering. When <c>null</c>,
        /// no load has occurred yet (fresh game). Call
        /// <see cref="ClearLoadState"/> to reset after processing.
        /// </summary>
        public HashSet<string> LoadedCompletedScenarioIds { get; set; }

        /// <summary>
        /// Clears the post-load scenario block list. Should be called
        /// after the scenario runner has consumed the IDs to prevent
        /// stale data from persisting across scene transitions.
        /// </summary>
        public void ClearLoadState()
        {
            LoadedCompletedScenarioIds = null;
        }

        /// <summary>
        /// Resolves the currently active scene into a
        /// <see cref="Cthangover.Core.Scenes.GodotSceneType"/> enum value.
        /// Queries <see cref="Cthangover.Core.Scenes.SceneManager"/> first,
        /// then falls back to parsing the current scene's resource path.
        /// Defaults to <c>GodotSceneType.MainMenu</c> when unresolvable.
        /// </summary>
        public GodotSceneType CurrentScene
        {
            get
            {
                var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
                if (sceneManager != null && !string.IsNullOrEmpty(sceneManager.CurrentSceneName))
                {
                    if (System.Enum.TryParse<GodotSceneType>(sceneManager.CurrentSceneName, true, out var result))
                        return result;
                }

                var scene = SceneContextNode.CurrentScene;
                if (scene != null)
                {
                    var path = scene.SceneFilePath;
                    if (!string.IsNullOrEmpty(path))
                    {
                        var name = System.IO.Path.GetFileNameWithoutExtension(path);
                        if (!string.IsNullOrEmpty(name) && System.Enum.TryParse<GodotSceneType>(name, true, out var result))
                            return result;
                    }
                }
                return GodotSceneType.MainMenu;
            }
        }
    }
    
}
