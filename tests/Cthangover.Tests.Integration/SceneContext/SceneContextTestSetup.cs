#if TOOLS
namespace Cthangover.Core.Autotest
{
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Runtime checks of the scene lookup cache (<see cref="SceneContextNode"/>)
    /// and of <see cref="SceneManager.SwitchScene"/> for a scene without any
    /// event: lookups by name are typed, so one type's miss or hit must not
    /// decide another type's answer, and every scene switch refreshes the save
    /// icon, whatever the scene holds.
    /// </summary>
    public partial class SceneContextTestSetup : Node
    {
        private const string Category = "SCENE_CTX_TEST";

        private int failed;

        public override void _Ready() => Callable.From(Run).CallDeferred();

        private void Run()
        {
            var probe = new SaveIconProbe { Name = "TopView" };
            GetTree().Root.AddChild(probe);
            var label = new Label { Name = "SceneCtxTestLabel" };
            AddChild(label);
            SceneContextNode.Instance.ClearData();

            Check("typed miss does not hide the node from a matching type",
                SceneContextNode.FindNode<Button>("SceneCtxTestLabel") == null
                && SceneContextNode.FindNode<Label>("SceneCtxTestLabel") == label);
            Check("cached node asked as another type is a miss, not an exception",
                TryFind<Button>("SceneCtxTestLabel") && SceneContextNode.FindNode<Control>("SceneCtxTestLabel") == label);

            var sceneManager = GetTree().Root.GetNode<SceneManager>("SceneManager");
            var before = probe.Updates;
            sceneManager.SwitchScene("scene_ctx_test_empty");
            Check("switch into a scene without events refreshes the save icon", probe.Updates > before);

            probe.QueueFree();
            GameLogger.Log(Category, $"[TEST_RESULT] name=scene_context passed={(failed == 0 ? 1 : 0)} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }

        private static bool TryFind<T>(string name) where T : Node
        {
            try
            {
                return SceneContextNode.FindNode<T>(name) == null;
            }
            catch (System.Exception ex)
            {
                GameLogger.Log(Category, $"FindNode<{typeof(T).Name}>('{name}') threw {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                GameLogger.Log(Category, $"PASS {what}");
                return;
            }
            failed++;
            GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
        }
    }
}
#endif
