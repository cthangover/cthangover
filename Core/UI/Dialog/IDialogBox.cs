using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Executable;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Dialog box contract: exposes the runtime for inspection, avatar/text/title
    /// setters for action commands to push visual state, and SetDialogQueueAndRun
    /// for starting a dialog sequence. SelectVariant is called by answer items
    /// when the player picks a choice.
    /// </summary>
    public interface IDialogBox : IWidget
    {
        DialogRuntime Runtime { get; }
        bool IsAnswerBoxShowed { get; }

        void SelectVariant(Action.Impls.SelectVariant variant);
        void SetVariants(System.Collections.Generic.ICollection<Action.Impls.SelectVariant> variants);

        void SetText(string text);
        void SetTitle(string title);
        void SetFirstAvatar(Texture2D avatar, bool hideColor = false);
        void SetSecondAvatar(Texture2D avatar, bool hideColor = false);

        void SetDialogQueueAndRun(DialogQueue dialog, System.Collections.Generic.IEnumerable<IActionCommand> endDialogQueue, int startIndex, ExecutableEvent locker);
        void NextAction();
    }
}
