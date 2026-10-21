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
using Cthangover.Mods.Interface.View;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Spawns a visual effect from the EffectFactory by ID (or a ready
    /// <see cref="Prefab"/>, bypassing the factory like ActionBackground.Texture).
    /// The scene is instantiated on construction and put into the ViewBox's UI
    /// layer (above all scene visuals, below the dialog) when the action runs; the
    /// node persists until the action is destructed. Uses DestructType.OnDelayed
    /// because the effect must outlive the action's run: the runtime frees it when
    /// the dialog ends (End destructs every constructed action), unless the effect
    /// node frees itself first.
    /// </summary>
    public class ActionEffect : ActionCommand
    {
        /// <summary>Identifier resolved through <see cref="EffectFactory"/> to a PackedScene. The spawned node persists until destructed.</summary>
        public string EffectID { get; set; }

        /// <summary>Effect scene to spawn instead of resolving <see cref="EffectID"/>; null resolves through the factory.</summary>
        public PackedScene Prefab { get; set; }

        private Node spawnedEffect;

        /// <summary>The dialog continues immediately — the spawned effect plays independently.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;
        /// <summary>OnDelayed keeps the effect node alive past the action's run; it is freed when the dialog ends.</summary>
        public override DestructType DestructType { get; set; } = DestructType.OnDelayed;

        /// <summary>Resolves the effect scene (<see cref="Prefab"/> or <see cref="EffectID"/> through <see cref="EffectFactory"/>) and instantiates it.</summary>
        public override void DoConstruct()
        {
            base.DoConstruct();

            var packedScene = Prefab;
            if (packedScene == null)
            {
                if (string.IsNullOrEmpty(EffectID))
                    return;

                packedScene = EffectFactory.Instance.Get(EffectID);
                if (packedScene == null)
                {
                    GameLogger.Log("DIALOG", $"ActionEffect: effect '{EffectID}' not found", LogLevel.Error);
                    return;
                }
            }

            spawnedEffect = packedScene.Instantiate();
        }

        /// <summary>Shows the instantiated effect: adds it to the ViewBox UI layer. Without a ViewBox the effect is dropped with an error.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (spawnedEffect == null || spawnedEffect.IsInsideTree())
                return;

            var layer = Scene.Find<ViewBox>("ViewBox")?.GetInteractiveLayer("ui");
            if (layer == null)
            {
                GameLogger.Log("DIALOG", $"ActionEffect: no ViewBox to show effect '{EffectID}'", LogLevel.Error);
                spawnedEffect.QueueFree();
                spawnedEffect = null;
                return;
            }

            layer.AddChild(spawnedEffect);
        }

        /// <summary>Frees the spawned effect node (unless it already freed itself); paired with the OnDelayed destruct so effects outlive the action's run.</summary>
        public override void DoDestruct()
        {
            base.DoDestruct();
            if (spawnedEffect != null)
            {
                if (GodotObject.IsInstanceValid(spawnedEffect))
                    spawnedEffect.QueueFree();
                spawnedEffect = null;
            }
        }
    }
}
