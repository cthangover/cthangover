using System.Collections.Generic;
using Cthangover.Mods.Interface.Base.Lists.Impls;
using Cthangover.Mods.Dialog.Action.Impls;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Answer choice container: extends VerticalListWidget but deliberately
    /// overrides PutToLayout to do nothing — the prefab-based layout system
    /// handles positioning internally (answer_box.tscn: Content VBoxContainer
    /// lays out the items as a container), and the base ColumnCellListWidget
    /// layout would conflict. Content size is computed as count * 60px height,
    /// creating a fixed-height per-option layout. Self-destructs on hide.
    /// </summary>
    public partial class AnswerBox : VerticalListWidget<AnswerItem, SelectVariant>
    {

        private ICollection<SelectVariant> variants;

        /// <summary>answer_box.tscn binds Content via the content export; the lookup covers instances built without the binding.</summary>
        protected override void OnReady()
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
