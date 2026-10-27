namespace Cthangover.Mods.Crafting
{
    using System;
    using Godot;

    /// <summary>A data-driven recipe row carries its id through the parameterless Godot pressed signal.</summary>
    public partial class CraftRecipeButton : Button
    {
        /// <summary>Recipe selected by this row.</summary>
        public string RecipeId { get; set; }

        /// <summary>Raised by a player click or keyboard activation.</summary>
        public event Action<string> Selected;

        /// <summary>Uses a named handler so rows do not retain closures over the recipe list.</summary>
        public CraftRecipeButton() => Pressed += OnPressed;

        private void OnPressed() => Selected?.Invoke(RecipeId);
    }
}
