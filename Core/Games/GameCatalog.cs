using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Games
{
    /// <summary>
    /// What the menus know about games, computed against every installed mod
    /// (not only the loaded ones — the front-end loads almost nothing): the
    /// game collections with their readiness, the composition a game would
    /// run with now (for comparing with a save) and the cover image.
    /// </summary>
    public static class GameCatalog
    {
        /// <summary>Merged collections (user over built-in over catalog), keyed by id.</summary>
        public static Dictionary<string, ModCollection> LoadCollections()
        {
            var result = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in ModCollectionStore.LoadAll())
                result[collection.Id] = collection;
            return result;
        }

        /// <summary>One summary per game collection, ordered by name; '@key' text resolved.</summary>
        public static List<ModCollectionGameSummary> Games(IReadOnlyCollection<string> sessionInstalled = null)
            => ModCollectionGallery.Build(
                LoadCollections(),
                ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(),
                CatalogService.CachedEntries ?? new List<CatalogExportEntry>(),
                sessionInstalled ?? Array.Empty<string>(),
                CollectionText.Resolve,
                TranslationServer.GetLocale());

        /// <summary>
        /// Mods (with versions) <paramref name="gameId"/> runs with now: the
        /// loaded set for the active game, otherwise the set its collection
        /// resolves to among installed mods.
        /// </summary>
        public static List<SaveGameMod> Composition(string gameId)
        {
            var discovered = ModRegistry.Instance.AllDiscoveredMods;
            IEnumerable<string> ids;
            if (ActiveGame.IsActive && string.Equals(ActiveGame.Id, gameId, StringComparison.OrdinalIgnoreCase))
            {
                ids = ModRegistry.Instance.GetOrderedModIds();
            }
            else
            {
                var set = GameModSet.Compute(gameId, LoadCollections(), discovered, DisabledModsStore.Load());
                if (set == null)
                    return null;
                ids = set.ModIds.OrderBy(id => id, StringComparer.Ordinal);
            }

            return ids.Select(id => new SaveGameMod
            {
                Id = id,
                Version = discovered.TryGetValue(id, out var info) ? info.Version : null,
            }).ToList();
        }

        /// <summary>
        /// True when the running game no longer matches its collection: the
        /// player edited it (in the menu's collection editor) after the game was
        /// entered, so the mod set it would boot with now differs from the loaded
        /// one, or its definition (entry scene, settings) changed. The menus then
        /// enter the game through an in-process mod-set switch instead of
        /// restarting it on the stale set. False without an active game.
        /// </summary>
        public static bool IsActiveOutdated()
        {
            if (!ActiveGame.IsActive)
                return false;

            var collections = LoadCollections();
            if (!collections.TryGetValue(ActiveGame.Id, out var current))
                return true;

            var extras = new List<string>(GameLaunchArgs.Current.WithMods);
            extras.AddRange(ModTestSuites.ActiveIds);
            var set = GameModSet.Compute(ActiveGame.Id, collections, ModRegistry.Instance.AllDiscoveredMods, DisabledModsStore.Load(), extras);
            if (set == null || !set.ModIds.SetEquals(ActiveGame.ModIds))
                return true;

            return JsonSerializer.Serialize(current) != JsonSerializer.Serialize(ActiveGame.Collection);
        }

        /// <summary>The collection's cover from its package, or null.</summary>
        public static Texture2D LoadCover(ModCollection collection)
        {
            if (string.IsNullOrEmpty(collection?.Cover) || collection.Package == null)
                return null;

            try
            {
                var bytes = collection.Package.ReadFileBinary(collection.Cover);
                if (bytes == null || bytes.Length == 0)
                    return null;

                var image = new Image();
                var extension = Path.GetExtension(collection.Cover).ToLowerInvariant();
                var error = extension switch
                {
                    ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
                    ".webp" => image.LoadWebpFromBuffer(bytes),
                    _ => image.LoadPngFromBuffer(bytes),
                };
                return error == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
            }
            catch (Exception ex)
            {
                GameLogger.Log("GAME", $"Cover '{collection.Cover}' of '{collection.Id}' failed to load: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }
    }
}
