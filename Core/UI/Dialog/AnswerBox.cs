using System.Collections.Generic;
using Cthangover.Core.UI.Base.Lists.Impls;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Answer choice container: extends VerticalListWidget but deliberately
    /// overrides PutToLayout to do nothing — the prefab-based layout system
    /// handles positioning internally, and the base ColumnCellListWidget layout
    /// would conflict. Content size is computed as count * 60px height, creating
    /// a fixed-height per-option layout. Self-destructs on hide.
    /// </summary>
    public partial class AnswerBox : VerticalListWidget<AnswerItem, SelectVariant>
    {

        private ICollection<SelectVariant> variants;

        public override void _Ready()
        {
            if (Content == null)
            {
                var found = FindChild("Content", true, false);
                if (found != null)
                    Set("content", found);
            }
        }

        /// <summary>Stores the variants and triggers <see cref="Widget.Show"/> to build and display the answer list.</summary>
        public void CreateVariantsUI(ICollection<SelectVariant> variants)
        {
            this.variants = variants;
            Show();
        }

        protected override void HideDestruct()
        {
            base.HideDestruct();
            QueueFree();
        }

        /// <summary>Returns the stored variants collection. Called by the base <see cref="ListWidget{T, M}.ConstructUI"/> pipeline.</summary>
        public override ICollection<SelectVariant> CreateModels()
        {
            return variants;
        }

        /// <summary>Deliberate no-op — answer items are positioned by their own layout system, not by the list widget's PutToLayout.</summary>
        protected override void PutToLayout(AnswerItem item, int index, Control container, Vector2 contentSize)
        {
        }

        /// <summary>Fixed-height layout: content height = <paramref name="count"/> * 60px. Width is inherited from the Content container.</summary>
        public override Vector2 GetContentSize(int count)
        {
            return new Vector2(Content.Size.X, count * 60);
        }

    }

}
