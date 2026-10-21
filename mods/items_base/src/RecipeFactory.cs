using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Factory for crafting recipes. Uses <c>BoundedCache</c> because the
    /// crafting UI may repeatedly query recipes during scrolling and the
    /// ingredient resolution (through <c>ItemFactory</c>) is non-trivial.
    /// Recipes are treated as immutable value objects — <c>Get</c> returns
    /// the cached instance directly without copying. Missing ingredient
    /// items emit an error and skip that ingredient rather than failing the
    /// whole recipe, so a mod author's typo in one input doesn't silently
    /// break the crafting UI for unrelated recipes.
    /// <para/>
    /// The single instance lives in this mod assembly as a static lazy
    /// singleton; the static facade stays as the public API.
    /// </summary>
    public class RecipeFactory : ICacheLoader<string, IRecipe>
    {
        private static RecipeFactory _instance;

        /// <summary>Static lazy singleton; kept as the public API.</summary>
        public static RecipeFactory Instance => _instance ??= new RecipeFactory();

        private Dictionary<string, RecipeInfo> _allInfos;
        private readonly BoundedCache<string, IRecipe> _cache;

        internal RecipeFactory()
        {
            var size = ModConfig.Instance.Cache.GetCacheSize("recipes", 128);
            _cache = new BoundedCache<string, IRecipe>(size, this);
        }

        IRecipe ICacheLoader<string, IRecipe>.Load(string id)
        {
            var infos = EnsureInfos();
            if (infos.TryGetValue(id, out var info))
                return CreateRecipe(info);
            return null;
        }

        /// <summary>
        /// Returns the recipe definitions, collecting them on first use.
        /// Publication to <c>_allInfos</c> waits until the mod registry is
        /// finalized so an early lookup from the launcher boot window cannot
        /// freeze an empty snapshot for the session.
        /// </summary>
        private Dictionary<string, RecipeInfo> EnsureInfos()
        {
            // The resolver's dictionary identity is the data snapshot: a new one drops the built recipes.
            var infos = Entities.Collect<RecipeInfo>();
            if (!ReferenceEquals(infos, _allInfos))
            {
                _allInfos = infos;
                _cache.Clear();
            }
            return infos;
        }

        /// <summary>
        /// Returns the cached recipe for <paramref name="id"/> (resolved lazily
        /// through <see cref="RecipeFactory.Load"/>), or <c>null</c> for unknown
        /// or empty ids. The shared instance is returned without copying —
        /// recipes are treated as immutable.
        /// </summary>
        public IRecipe Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInfos();
            return _cache.Get(id);
        }

        private static Recipe CreateRecipe(RecipeInfo info)
        {
            return new Recipe
            {
                ID = info.ID,
                Name = info.Name,
                Description = info.Description,
                Time = info.Time,
                WorkbenchType = info.Type,
                Input = info.Input
                    .Select(i => CreateIngredient(i))
                    .Where(i => i != null)
                    .ToList(),
                Output = info.Output
                    .Select(i => CreateIngredient(i))
                    .Where(i => i != null)
                    .ToList()
            };
        }

        private static IIngredient CreateIngredient(IngredientInfo info)
        {
            var item = ItemFactory.Instance.Get(info.ID);
            if (item == null)
            {
                GameLogger.Log("MOD_TEST", $"RecipeFactory: item '{info.ID}' not found in ItemFactory", LogLevel.Error);
                return null;
            }
            return new Ingredient { Item = item, Count = info.Count };
        }
    }
}
