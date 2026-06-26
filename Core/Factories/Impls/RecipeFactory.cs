using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
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
    /// </summary>
    public class RecipeFactory : ICacheLoader<string, IRecipe>
    {
        private static readonly Lazy<RecipeFactory> lazy = new(() => new RecipeFactory());
        public static RecipeFactory Instance => lazy.Value;

        private Dictionary<string, RecipeInfo> _allInfos;
        private readonly BoundedCache<string, IRecipe> _cache;

        private RecipeFactory()
        {
            var size = ModConfig.Instance.Cache.GetCacheSize("recipes", 128);
            _cache = new BoundedCache<string, IRecipe>(size, this);
        }

        IRecipe ICacheLoader<string, IRecipe>.Load(string id)
        {
            EnsureInfos();
            if (_allInfos.TryGetValue(id, out var info))
                return CreateRecipe(info);
            return null;
        }

        private void EnsureInfos()
        {
            if (_allInfos != null)
                return;
            _allInfos = ModManager.Instance.CollectJsonGroup<RecipeInfo>("recipes");
        }

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
