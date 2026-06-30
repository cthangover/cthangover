using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Godot;

namespace Mods.Cooking.Workbench
{
    /// <summary>
    /// Represents a single recipe entry in the <see cref="WorkbenchPanel"/> list.
    /// Displays the recipe name, preparation time, and a horizontal row of
    /// ingredient icons. Supports click-to-select with visual highlight
    /// (tracked statically so only one recipe is selected at a time).
    /// Fires <see cref="RecipeClicked"/> to notify the parent panel of
    /// selection changes. Ingredient availability is checked via child
    /// <see cref="RecipeIconItemBehaviour"/> instances, greying out the
    /// entire row when any ingredient is missing.
    /// </summary>
    public class RecipeItemBehaviour : Widget
    {
        private Control background;
        private Control recipeContent;
        private Label txtName;
        private Label txtTime;
        private HBoxContainer recipeContentHbox;

        private static readonly Color normalColor = new(1f, 1f, 1f, 1f);
        private static readonly Color selectedColor = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color disableColor = new(0.5f, 0.5f, 0.5f, 1f);

        private IRecipe recipe;
        private Node panel;

        private static RecipeItemBehaviour selected;

        /// <summary>
        /// The recipe currently selected across all <see cref="RecipeItemBehaviour"/>
        /// instances. Only one recipe can be selected at a time because the
        /// selection is tracked via a static backing field. Returns <c>null</c>
        /// when no recipe is selected.
        /// </summary>
        public static IRecipe SelectedRecipe => selected?.recipe;
        private readonly List<Node> items = new();
        private readonly List<RecipeIconItemBehaviour> ingredients = new();

        /// <summary>
        /// Fired when this recipe entry is clicked, passing the recipe data
        /// and this widget so that <see cref="WorkbenchPanel"/> can update
        /// the description and output preview panel.
        /// </summary>
        public event System.Action<IRecipe, RecipeItemBehaviour> RecipeClicked;

        protected override void OnceConstruct()
        {
            CustomMinimumSize = new Vector2(0, 30);

            background = new Control();
            background.MouseFilter = MouseFilterEnum.Ignore;
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(background);

            var hbox = new HBoxContainer();
            hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(hbox);

            recipeContent = new Control();
            recipeContent.CustomMinimumSize = new Vector2(100, 30);
            recipeContent.Size = new Vector2(100, 30);
            hbox.AddChild(recipeContent);

            recipeContentHbox = new HBoxContainer();
            recipeContent.AddChild(recipeContentHbox);

            txtName = new Label();
            txtName.AddThemeColorOverride("font_color", Colors.Yellow);
            txtName.AddThemeFontSizeOverride("font_size", 12);
            txtName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(txtName);

            txtTime = new Label();
            txtTime.AddThemeColorOverride("font_color", Colors.Red);
            txtTime.AddThemeFontSizeOverride("font_size", 12);
            txtTime.CustomMinimumSize = new Vector2(40, 30);
            hbox.AddChild(txtTime);

            GuiInput += OnGuiInput;
        }

        protected override void OnceDestruct()
        {
            GuiInput -= OnGuiInput;
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                OnClick();
            }
        }

        /// <summary>
        /// Refreshes the visual availability state of this recipe.
        /// Iterates all child <see cref="RecipeIconItemBehaviour"/> widgets,
        /// calling <c>CheckAndUpdate</c> on each. If any ingredient is missing,
        /// tints the name and time labels with <c>disableColor</c> (greyed out);
        /// otherwise restores the default yellow/red colour scheme.
        /// Call after inventory changes or cooking operations.
        /// </summary>
        public void UpdateState()
        {
            var hasItems = true;

            foreach (var ingredient in ingredients)
                if (!ingredient.CheckAndUpdate())
                    hasItems = false;

            if (!hasItems)
            {
                txtName.AddThemeColorOverride("font_color", disableColor);
                txtTime.AddThemeColorOverride("font_color", disableColor);
            }
            else
            {
                txtName.AddThemeColorOverride("font_color", Colors.Yellow);
                txtTime.AddThemeColorOverride("font_color", Colors.Red);
            }
        }

        /// <summary>
        /// Initialises the recipe row with recipe data and the parent panel
        /// reference. Spawns <see cref="RecipeIconItemBehaviour"/> children
        /// for each ingredient, with plus-sign separators between them.
        /// Sets the translated recipe name and time, then calls
        /// <see cref="UpdateState"/> to set initial colour state.
        /// </summary>
        public void Init(IRecipe recipe, Node panel)
        {
            EnsureConstructed();
            this.recipe = recipe;
            this.panel = panel;

            for (int i = 0; i < recipe.Input.Count; i++)
            {
                var ingredient = recipe.Input[i];
                AddIngredient(ingredient, i != recipe.Input.Count - 1);
            }

            txtName.Text = TranslationServer.Translate(recipe.Name);
            txtTime.Text = string.Format(TranslationServer.Translate("ui/cook/recipe_time"), recipe.Time);

            UpdateState();
        }

        private void AddIngredient(IIngredient ingredient, bool addPlus)
        {
            var inventory = GameData.Instance.Runtime.Inventory;

            var item = new RecipeIconItemBehaviour();
            recipeContentHbox.AddChild(item);
            var localHasItems = inventory.CheckCount(ingredient.Item) < ingredient.Count;
            item.Init(ingredient, localHasItems);
            items.Add(item);
            ingredients.Add(item);

            if (addPlus)
            {
                var plus = new Label();
                plus.Text = "+";
                plus.AddThemeFontSizeOverride("font_size", 12);
                plus.VerticalAlignment = VerticalAlignment.Center;
                recipeContentHbox.AddChild(plus);
                items.Add(plus);
            }
        }

        /// <summary>
        /// Tears down this recipe row by queue-freeing all child nodes
        /// (ingredient icons, plus labels, and this widget itself),
        /// then clears the internal ingredient/item lists.
        /// Called by <see cref="WorkbenchPanel"/> when the panel is hidden.
        /// </summary>
        public void Destroy()
        {
            foreach (var item in items)
                item.QueueFree();
            QueueFree();
            ingredients.Clear();
            items.Clear();
        }

        /// <summary>
        /// Handles left-click selection of this recipe entry.
        /// Deselects the previously-selected entry (if any) by restoring its
        /// background to normal colour, highlights the background of this
        /// entry, updates the static <c>selected</c> tracker, and invokes
        /// <see cref="RecipeClicked"/> so the parent can update the
        /// description and output panel.
        /// </summary>
        public void OnClick()
        {
            if (selected != null)
                selected.background.Modulate = normalColor;
            background.Modulate = selectedColor;
            selected = this;
            RecipeClicked?.Invoke(recipe, this);
        }
    }
}
