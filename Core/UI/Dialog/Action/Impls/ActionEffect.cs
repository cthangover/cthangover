using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionEffect : ActionCommand
    {
        public string EffectID { get; set; }

        private Node spawnedEffect;

        public override WaitType WaitType { get; set; } = WaitType.NoWait;
        public override DestructType DestructType { get; set; } = DestructType.OnDelayed;

        public override void DoConstruct()
        {
            base.DoConstruct();
            if (string.IsNullOrEmpty(EffectID))
                return;

            var packedScene = EffectFactory.Instance.Get(EffectID);
            if (packedScene == null)
            {
                GameLogger.Log("DIALOG", $"ActionEffect: effect '{EffectID}' not found", LogLevel.Error);
                return;
            }

            spawnedEffect = packedScene.Instantiate();
        }

        public override void DoRun(DialogRuntime runtime)
        {
        }

        public override void DoDestruct()
        {
            base.DoDestruct();
            if (spawnedEffect != null)
            {
                spawnedEffect.QueueFree();
                spawnedEffect = null;
            }
        }
    }
}
