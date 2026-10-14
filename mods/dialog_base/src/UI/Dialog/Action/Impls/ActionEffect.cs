using Cthangover.Mods.Effects;
using Cthangover.Core.Utils;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Spawns a visual effect from the EffectFactory by ID. The spawned node
    /// persists until the action is destructed. Uses DestructType.OnDelayed
    /// because the effect must outlive the action's run — the effect node's own
    /// animation or lifetime controls when it disappears. DoRun is a no-op
    /// because construction handles spawning.
    /// </summary>
    public class ActionEffect : ActionCommand
    {
        /// <summary>Identifier resolved through <see cref="EffectFactory"/> to a PackedScene. The spawned node persists until destructed.</summary>
        public string EffectID { get; set; }

        private Node spawnedEffect;

        /// <summary>The dialog continues immediately — the spawned effect plays independently.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;
        /// <summary>OnDelayed ensures the effect node outlives the action's run phase. Destruct is called manually when the effect finishes.</summary>
        public override DestructType DestructType { get; set; } = DestructType.OnDelayed;

        /// <summary>Resolves <see cref="EffectID"/> through <see cref="EffectFactory"/> and instantiates the effect scene.</summary>
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

        /// <summary>No-op: spawning happens during construction so the effect is ready before the queue reaches this point.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
        }

        /// <summary>Frees the spawned effect node; paired with the OnDelayed destruct so effects outlive the action's run.</summary>
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
