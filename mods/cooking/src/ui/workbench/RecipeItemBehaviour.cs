using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Godot;

namespace Mods.Cooking.Workbench
{
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
        public static IRecipe SelectedRecipe => selected?.recipe;
        private readonly List<Node> items = new();
        private readonly List<RecipeIconItemBehaviour> ingredients = new();

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

        public void Destroy()
        {
            foreach (var item in items)
                item.QueueFree();
            QueueFree();
            ingredients.Clear();
            items.Clear();
        }

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
