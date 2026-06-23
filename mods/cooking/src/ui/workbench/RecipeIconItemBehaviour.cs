using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.Cooking.Workbench
{
    public class RecipeIconItemBehaviour : Widget
    {
        private static readonly Color NormalColor = Colors.White;
        private static readonly Color NotItemsColor = Colors.Gray;

        private TextureRect img;
        private Label txt;
        private IIngredient ingredient;

        protected override void OnceConstruct()
        {
            img = new TextureRect();
            img.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            img.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            img.CustomMinimumSize = new Vector2(32, 32);
            img.Size = new Vector2(32, 32);
            AddChild(img);

            txt = new Label();
            txt.HorizontalAlignment = HorizontalAlignment.Right;
            txt.VerticalAlignment = VerticalAlignment.Bottom;
            txt.AddThemeFontSizeOverride("font_size", 10);
            txt.Position = new Vector2(24, 20);
            AddChild(txt);
        }

        public void Init(IIngredient ingredient, bool hasItems)
        {
            EnsureConstructed();
            this.ingredient = ingredient;
            if (ingredient.Item != null)
            {
                img.Texture = ingredient.Item.Sprite;
            }
            else
            {
                GameLogger.Log("MOD_TEST", $"RecipeIconItemBehaviour::Init ingredient.Item is NULL", LogLevel.Error);
            }
            txt.Text = ingredient.Count.ToString();
        }

        public bool CheckAndUpdate()
        {
            var result = GameData.Instance.Runtime.Inventory.CheckCount(ingredient.Item) >= ingredient.Count;
            txt.AddThemeColorOverride("font_color", result ? NormalColor : NotItemsColor);
            img.Modulate = result ? Colors.White : NotItemsColor;
            return result;
        }
    }
}
