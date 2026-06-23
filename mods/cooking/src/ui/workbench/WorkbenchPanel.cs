using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Audio;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.UI.Inventory;
using Godot;

namespace Mods.Cooking.Workbench
{
    public class WorkbenchPanel : Widget
    {
        private Control content;
        private RichTextLabel txtDescription;
        private InventoryBagBehaviour outputContainer;

        private List<RecipeItemBehaviour> recipes = new();

        protected override void OnceConstruct()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            var bg = new TextureRect();
            bg.Texture = BackgroundFactory.Instance.Get("cook_table");
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            var hbox = new HBoxContainer();
            hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hbox.AddThemeConstantOverride("separation", 10);
            AddChild(hbox);

            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(400, 0);
            hbox.AddChild(leftPanel);

            var leftTitle = new Label();
            leftTitle.Text = TranslationServer.Translate("ui/cook/title");
            leftTitle.AddThemeFontSizeOverride("font_size", 16);
            leftPanel.AddChild(leftTitle);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftPanel.AddChild(scroll);

            content = new VBoxContainer();
            content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(content);

            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(300, 0);
            hbox.AddChild(rightPanel);

            txtDescription = new RichTextLabel();
            txtDescription.BbcodeEnabled = true;
            txtDescription.ScrollActive = true;
            txtDescription.CustomMinimumSize = new Vector2(0, 100);
            txtDescription.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.AddChild(txtDescription);

            outputContainer = new InventoryBagBehaviour();
            outputContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            outputContainer.Visible = false;
            rightPanel.AddChild(outputContainer);

            var btnCook = new Button();
            btnCook.Text = TranslationServer.Translate("ui/cook/cook");
            btnCook.Pressed += OnCookClick;
            rightPanel.AddChild(btnCook);

            var btnClose = new Button();
            btnClose.Text = TranslationServer.Translate("ui/cook/close");
            btnClose.Pressed += OnCloseClick;
            rightPanel.AddChild(btnClose);
        }

        protected override void ShowConstruct()
        {
            foreach (var recipe in GetSortedList())
                AddItem(recipe);
        }

        protected override void HideDestruct()
        {
            foreach (var recipeItem in recipes)
                if (recipeItem != null)
                    recipeItem.QueueFree();
            recipes.Clear();
        }

        private void AddItem(IRecipe recipe)
        {
            var item = new RecipeItemBehaviour();
            if (item != null && content != null)
            {
                content.AddChild(item);
                item.Init(recipe, this);
                item.RecipeClicked += ClickRecipe;
                recipes.Add(item);
            }
        }

        private List<IRecipe> GetSortedList()
        {
            var list = GameData.Instance.Runtime.RecipeData.GetRecipesByType(WorkbenchType.Cooking);
            var inventory = GameData.Instance.Runtime.Inventory;
            list.Sort((o1, o2) =>
            {
                var hasNotItems1 = o1.Input.Any(o => inventory.CheckCount(o.Item) < o.Count);
                var hasNotItems2 = o2.Input.Any(o => inventory.CheckCount(o.Item) < o.Count);
                if (hasNotItems1 == hasNotItems2)
                    return string.Compare(o1.ID, o2.ID, StringComparison.Ordinal);
                return hasNotItems1 ? 1 : -1;
            });
            return list;
        }

        public void ClickRecipe(IRecipe recipe, RecipeItemBehaviour recipeItemBehaviour)
        {
            if (txtDescription != null)
            txtDescription.Text = "[b][color=yellow]" + TranslationServer.Translate(recipe.Name) + "[/color][/b]\n\n" +
                TranslationServer.Translate(recipe.Description);
            
            outputContainer.List = recipe.Output.Select(o => (IItemContainer)new ItemContainer
            {
                Item = o.Item,
                Count = o.Count,
            }).ToList();
        }

        public void OnCloseClick()
        {
            Hide();
            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PlaySound("ui/close_click", SoundType.UI);
        }

        public void OnCookClick()
        {
            var inventory = GameData.Instance.Runtime.Inventory;
            var receipt = RecipeItemBehaviour.SelectedRecipe;
            if (receipt == null)
                return;

            var hasItems = !receipt.Input.Any(o => inventory.CheckCount(o.Item) < o.Count);
            if (!hasItems)
                return;

            foreach (var item in receipt.Input)
                inventory.Remove(item.Item, item.Count);
            foreach (var item in receipt.Output)
                inventory.Add(item.Item, item.Count);

            GameData.Instance.Runtime.Time.AddTime(0, 0, 0, 0, receipt.Time);
            var timer = SceneContextNode.FindNode<TimeController>("TimeController");
            timer?.UpdateRenderedTime();

            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PlaySound("ui/cook", SoundType.UI);

            foreach (var recipe in recipes)
                recipe.UpdateState();
        }
    }
}
