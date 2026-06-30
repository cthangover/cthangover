using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.Cooking.Workbench
{
    /// <summary>
    /// Renders a single ingredient row inside a <see cref="RecipeItemBehaviour"/>
    /// on the cooking workbench. Displays the ingredient's item icon and the
    /// required count. When <see cref="CheckAndUpdate"/> is called, compares
    /// inventory count against the recipe requirement and tints the icon and
    /// count text grey if the player lacks enough items.
    /// </summary>
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

        /// <summary>
        /// Binds this icon widget to a recipe ingredient definition.
        /// Sets the icon texture from <c>ingredient.Item.Sprite</c> (logs an error
        /// if the item reference is null) and displays the required count as text.
        /// The <paramref name="hasItems"/> flag is currently unused but reserved
        /// for future visual pre-initialization.
        /// </summary>
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

        /// <summary>
        /// Evaluates whether the player's inventory contains enough of the
        /// required ingredient. Tints the icon and count text to
        /// <c>NormalColor</c> (white) when satisfied or <c>NotItemsColor</c>
        /// (grey) when insufficient. Called by the parent
        /// <see cref="RecipeItemBehaviour"/> during state refresh.
        /// </summary>
        /// <returns><c>true</c> if inventory meets or exceeds the required count.</returns>
        public bool CheckAndUpdate()
        {
            var result = GameData.Instance.Runtime.Inventory.CheckCount(ingredient.Item) >= ingredient.Count;
            txt.AddThemeColorOverride("font_color", result ? NormalColor : NotItemsColor);
            img.Modulate = result ? Colors.White : NotItemsColor;
            return result;
        }
    }
}
