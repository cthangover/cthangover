namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Scenes;

    /// <summary>Requests a normal logical scene transition from the live process.</summary>
    public sealed class ModDevelopmentSceneCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "scene.switch" };

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var scene = context.Require("scene");
            var scenes = context.Tree.Root.GetNode<SceneManager>("SceneManager");
            if (!scenes.HasScene(scene))
                throw new ArgumentException("Unknown logical scene: " + scene);
            scenes.SwitchScene(scene);
            context.MarkChanged();
            return Task.FromResult<JsonNode>(new JsonObject { ["requested"] = scene, ["scene"] = scenes.CurrentSceneName });
        }
    }
}
