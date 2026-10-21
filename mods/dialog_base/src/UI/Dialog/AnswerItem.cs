using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Dialog.Action.Impls;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Renders a single dialog choice option. Walks up the scene tree in OnReady
    /// to find the nearest IDialogBox parent — this avoids coupling to a specific
    /// dialog implementation and lets answer items work regardless of nesting depth.
    /// On click, calls SelectVariant on the found dialog box, passing the bound
    /// SelectVariant model (which carries the GoTo target and display text).
    /// </summary>
    public partial class AnswerItem : ListItem<SelectVariant>
    {

        [Export] private Label textField;

        private DialogBox dialogBox;
        
        /// <summary>Binds the choice button from the answer_item scene prefab and finds the owning dialog box by walking up the tree, so items work at any nesting depth.</summary>
        protected override void OnReady()
        {
            textField ??= GetNode<Label>("Button/TextLabel");

            var btn = GetNode<Button>("Button");
            if (btn != null)
                btn.Pressed += OnClick;

            if (dialogBox == null)
            {
                var parent = GetParent();
                while (parent != null)
                {
                    if (parent is DialogBox db)
                    {
                        dialogBox = db;
                        break;
                    }
                    parent = parent.GetParent();
                }
            }
        }
        
        /// <summary>Binds the variant model and sets the button label text to <see cref="SelectVariant.Text"/>.</summary>
        public override void Construct(SelectVariant variant)
        {
            base.Construct(variant);
            textField.Text = variant.Text;
        }
        
        private void OnClick()
        {
            dialogBox?.SelectVariant(Model);
        }

        /// <summary>Required by <see cref="IListItem{TModel}"/>. Intentionally empty — the parent AnswerBox handles cleanup.</summary>
        public override void Destruct()
        { }
        
    }
    
}
