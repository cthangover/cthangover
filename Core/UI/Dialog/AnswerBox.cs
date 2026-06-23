using System.Collections.Generic;
using Cthangover.Core.UI.Base.Lists;
using Cthangover.Core.UI.Base.Lists.Impls;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.UI.Dialog
{

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

        public override ICollection<SelectVariant> CreateModels()
        {
            return variants;
        }

        protected override void PutToLayout(AnswerItem item, int index, Control container, Vector2 contentSize)
        {
        }

        public override Vector2 GetContentSize(int count)
        {
            return new Vector2(Content.Size.X, count * 60);
        }

    }

}
