using System;
using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Central mutable state bag for the entire game session. Every subsystem
    /// — time, characters, inventory, recipes, lamp and skills —
    /// lives here. <see cref="SaveService.Save"/> samples
    /// all these fields into a <see cref="SavePackage"/> DTO; <see cref="SaveService.Load"/>
    /// writes them back. The <see cref="CurrentScene"/> property resolves
    /// the active scene name into a <see cref="Cthangover.Core.Scenes.GodotSceneType"/>
    /// enum for conditional logic elsewhere.
    /// </summary>
    public class RuntimeData
    {
        /// <summary>In-game clock (minutes-based) driving day/night cycles.</summary>
        public TimeData Time { get; set; } = new TimeData(143546335);

        /// <summary>Recruited character roster with attributes and battle party set.</summary>
        public CharacterData  CharacterData  { get; set; } = new();
        /// <summary>Unlocked skills and their cooldown state.</summary>
        public SkillData      SkillData      { get; set; } = new();
        /// <summary>
        /// Factory producing the player inventory implementation. Installed by
        /// the items mod (<c>ItemsModInitializer</c>) at resources-ready; the
        /// property below invokes it on first access, so the state bag can be
        /// constructed before the mod set is final without freezing an empty
        /// inventory. A <c>null</c> factory (items mod absent) yields a null
        /// inventory.
        /// </summary>
        public static Func<IInventory> InventoryFactory { get; set; }

        /// <summary>Factory producing the unlocked-recipe state; see <see cref="InventoryFactory"/>.</summary>
        public static Func<IRecipeData> RecipeDataFactory { get; set; }

        private IInventory _inventory;
        private IRecipeData _recipeData;

        /// <summary>Player inventory of item stacks; lazily produced by <see cref="InventoryFactory"/> on first access.</summary>
        public IInventory  Inventory  { get => _inventory ??= InventoryFactory?.Invoke(); set => _inventory = value; }

        /// <summary>Unlocked recipe IDs and lookup helpers; lazily produced by <see cref="RecipeDataFactory"/>.</summary>
        public IRecipeData RecipeData { get => _recipeData ??= RecipeDataFactory?.Invoke(); set => _recipeData = value; }
        /// <summary>Lamp light radius and influence state.</summary>
        public LampData       LampData       { get; set; } = new();
        /// <summary>Persistent collection of action IDs available for assignment via the character panel.</summary>
        public ActionPoolData ActionPool     { get; set; } = new();

        /// <summary>
        /// IDs of one-run scenarios completed in the current game.
        /// Grows as one-run scenario dialogs finish (see
        /// <c>DialogRuntime.End</c>), is replaced by the save's
        /// <c>CompletedScenarios</c> blob on load and cleared when a
        /// new game starts (see <c>GameData.ResetRuntime</c>). The
        /// scenario runner filters scene events against this set, which
        /// makes <c>is_one_run: true</c> hold across scene revisits and
        /// save/load cycles instead of only within a single scene visit.
        /// </summary>
        public HashSet<string> CompletedScenarioIds { get; } = new();

        /// <summary>
        /// Story flags of this game (<c>flag name value</c> in a scenario,
        /// <c>ScenarioCondition.SetFlag</c> from code), read by scenario
        /// conditions such as <c>door == "open"</c>. Part of the game state: saved
        /// with the core slot and gone with a new game, unlike dialog variables,
        /// which live only while one dialog runs.
        /// </summary>
        public Dictionary<string, string> Flags { get; set; } = new();

        /// <summary>
        /// IDs of one-shot scenarios restored from a loaded save.
        /// Populated by <see cref="SaveService.Load"/> and consumed by
        /// the scenario runner to skip re-triggering. When <c>null</c>,
        /// no load has occurred yet (fresh game). Call
        /// <see cref="ClearLoadState"/> to reset after processing.
        /// </summary>
        public HashSet<string> LoadedCompletedScenarioIds { get; set; }

        /// <summary>
        /// Clears the post-load scenario block list. Should be called
        /// after the scenario runner has consumed the IDs to prevent
        /// stale data from persisting across scene transitions. The
        /// persistent completion history itself lives in
        /// <see cref="CompletedScenarioIds"/> and is not affected.
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
