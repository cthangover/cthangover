using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Executable;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
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
