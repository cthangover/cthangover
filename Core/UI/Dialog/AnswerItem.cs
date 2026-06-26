using Cthangover.Core.Audio;
using Cthangover.Core.UI.Base.Lists;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Renders a single dialog choice option. Walks up the scene tree in _Ready
    /// to find the nearest IDialogBox parent — this avoids coupling to a specific
    /// dialog implementation and lets answer items work regardless of nesting depth.
    /// On click, calls SelectVariant on the found dialog box, passing the bound
    /// SelectVariant model (which carries the GoTo target and display text).
    /// </summary>
    public partial class AnswerItem : ListItem<SelectVariant>
    {

        [Export] private Label textField;

        private IDialogBox dialogBox;
        
        public override void _Ready()
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
                    if (parent is IDialogBox db)
                    {
                        dialogBox = db;
                        break;
                    }
                    parent = parent.GetParent();
                }
            }
        }
        
        public override void Construct(SelectVariant variant)
        {
            base.Construct(variant);
            textField.Text = variant.Text;
        }
        
        private void OnClick()
        {
            dialogBox?.SelectVariant(Model);
        }

        public override void Destruct()
        { }
        
    }
    
}
