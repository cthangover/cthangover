using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Relationship;
using Cthangover.Core.Scenes;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Settings
{
    
    public class RuntimeData
    {
        public TimeData Time { get; set; } = new TimeData(143546335);

        public BattleData     BattleData     { get; set; } = null;
        public CharacterData  CharacterData  { get; set; } = new();
        public RecruitingData RecruitingData { get; set; } = new();
        public SkillData      SkillData      { get; set; } = new();
        public Inventory      Inventory      { get; set; } = new();
        public RecipeData     RecipeData     { get; set; } = new();
        public LampData       LampData       { get; set; } = new();

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
