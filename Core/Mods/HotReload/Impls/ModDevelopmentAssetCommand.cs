namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Refreshes resource lookup and lets owners update their live representations.</summary>
    public sealed class ModDevelopmentAssetCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "assets.refresh" };

        /// <inheritdoc />
        public string Description { get; } = "assets.refresh: refreshes resource lookup after asset file changes and lets owners refresh their live representations; path is mod-relative.";

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var mod = context.ModId(loaded: true);
            var path = context.Request["path"]?.GetValue<string>();
            if (path != null && (path.Length == 0 || path.Contains('\\') || path.Contains(':')
                || path.Split('/').Any(part => part is "" or "." or "..")))
                throw new ArgumentException("Use a mod-relative resource path.");
            var refreshed = ModManager.Instance.RefreshDevelopmentAssets(mod, path);
            context.MarkChanged();
            return Task.FromResult<JsonNode>(new JsonObject { ["mod"] = mod, ["path"] = path, ["live_textures"] = refreshed });
        }
    }
}
