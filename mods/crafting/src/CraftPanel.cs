namespace Cthangover.Mods.Crafting
{
    using System.Collections.Generic;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// Station browser with a recipe list, live ingredient counts and a product preview.
    /// The scene owns layout; rows are generated from the station's data. Inventory is
    /// rebound on every show because loading or starting a game replaces it.
    /// </summary>
    public partial class CraftPanel : Widget
    {
        /// <summary>Registry entry used by craft.open and the shell's overlay blocker.</summary>
        public const string EntryId = "crafting";
        private CraftStation station;
        private RecipeInfo selected;
        private IInventory inventory;
        private VBoxContainer recipes;
        private Label title;
        private Label description;
        private Label ingredients;
        private Label feedback;
        private TextureRect icon;
        private Button craft;
        private bool crafting;

        /// <summary>Switches stations without storing game state in the window.</summary>
        public void SetStation(CraftStation value)
        {
            station = value;
            selected = null;
            if (inventory != null)
                Rebuild();
        }

        protected override void OnceConstruct()
        {
            title = GetNode<Label>("Panel/Margin/Root/Header/Title");
            recipes = GetNode<VBoxContainer>("Panel/Margin/Root/Body/Scroll/Recipes");
            description = GetNode<Label>("Panel/Margin/Root/Body/Details/Description");
            ingredients = GetNode<Label>("Panel/Margin/Root/Body/Details/Ingredients");
            feedback = GetNode<Label>("Panel/Margin/Root/Feedback");
            icon = GetNode<TextureRect>("Panel/Margin/Root/Body/Details/Icon");
            craft = GetNode<Button>("Panel/Margin/Root/Body/Details/Craft");
            craft.Pressed += OnCraft;
            GetNode<Button>("Panel/Margin/Root/Header/Close").Pressed += OnClose;
            GetNode<ColorRect>("Backdrop").Color = new Color(0.015f, 0.025f, 0.03f, 0.72f);
            GetNode<Panel>("Panel").AddThemeStyleboxOverride("panel", UiKit.Frame(10, UiTokens.Surface0, UiTokens.Line));
            title.AddThemeColorOverride("font_color", UiTokens.AccentBright);
            ingredients.AddThemeColorOverride("font_color", UiTokens.Muted);
        }

        protected override void ShowConstruct()
        {
            if (inventory != null)
                inventory.Change -= OnChanged;
            inventory = GameData.Instance.Runtime.Inventory;
            inventory.Change += OnChanged;
            feedback.Text = Tr("crafting/hint");
            Rebuild();
        }

        protected override void HideDestruct()
        {
            if (inventory != null)
                inventory.Change -= OnChanged;
            inventory = null;
        }

        protected override void OnInput(InputEvent @event)
        {
            if (Visible && @event.IsActionPressed("ui_cancel"))
            {
                OnClose();
                GetViewport().SetInputAsHandled();
            }
        }

        private void OnClose() => UiRegistry.Toggle(EntryId);

        private void OnChanged(string id, int count)
        {
            if (!crafting)
                Rebuild();
        }

        private void Rebuild()
        {
            if (station == null || inventory == null)
                return;
            title.Text = Tr(station.Name ?? station.ID);
            foreach (var child in recipes.GetChildren())
            {
                recipes.RemoveChild(child);
                child.QueueFree();
            }
            var definitions = Entities.Collect<RecipeInfo>();
            selected = selected != null && definitions.TryGetValue(selected.ID, out var current) ? current : null;
            foreach (var id in station.Recipes ?? new List<string>())
            {
                if (!definitions.TryGetValue(id, out var recipe))
                    continue;
                selected ??= recipe;
                var button = new CraftRecipeButton
                {
                    RecipeId = id,
                    Text = Tr(recipe.Name ?? id),
                    Alignment = HorizontalAlignment.Left,
                    CustomMinimumSize = new Vector2(240, 64),
                    Icon = recipe.Output?.Count > 0 ? ItemFactory.Instance.Get(recipe.Output[0].ID)?.Sprite : null,
                    ExpandIcon = true,
                    ToggleMode = true,
                    ButtonPressed = selected.ID == id,
                };
                button.AddThemeConstantOverride("icon_max_width", 40);
                button.Selected += OnRecipe;
                recipes.AddChild(button);
            }
            ShowRecipe();
        }

        private void OnRecipe(string id)
        {
            Entities.Collect<RecipeInfo>().TryGetValue(id, out selected);
            Rebuild();
        }

        private void ShowRecipe()
        {
            craft.Text = Tr("crafting/create");
            craft.Disabled = !CraftLedger.CanCraft(inventory, selected);
            description.Text = selected == null ? Tr("crafting/empty") : Tr(selected.Description ?? selected.Name ?? selected.ID);
            icon.Texture = selected?.Output?.Count > 0 ? ItemFactory.Instance.Get(selected.Output[0].ID)?.Sprite : null;
            var lines = new List<string>();
            if (selected != null)
            {
                lines.Add(Tr("crafting/ingredients"));
                foreach (var entry in selected.Input ?? new List<IngredientInfo>())
                {
                    var item = ItemFactory.Instance.Get(entry.ID);
                    lines.Add($"{Tr(item?.Name ?? entry.ID)}   {inventory.CheckCount(entry.ID)} / {entry.Count}");
                }
                lines.Add("");
                lines.Add(Tr("crafting/products"));
                foreach (var entry in selected.Output ?? new List<IngredientInfo>())
                    lines.Add($"{Tr(ItemFactory.Instance.Get(entry.ID)?.Name ?? entry.ID)} × {entry.Count}");
            }
            ingredients.Text = string.Join("\n", lines);
        }

        private void OnCraft()
        {
            crafting = true;
            var success = CraftLedger.TryCraft(inventory, selected);
            crafting = false;
            if (success)
                GameData.Instance.Runtime.Time.AddTime(0, 0, 0, 0, selected.Time);
            feedback.Text = Tr(success ? "crafting/success" : "crafting/missing");
            Rebuild();
        }
    }
}
