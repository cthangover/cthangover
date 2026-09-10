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
        /// <summary>The execution engine driving the dialog sequence. Accessible for inspection by external systems.</summary>
        DialogRuntime Runtime { get; }
        /// <summary>Whether the answer/choice box is currently displayed.</summary>
        bool IsAnswerBoxShowed { get; }

        /// <summary>Called by answer items when the player selects a choice. Forwards the variant's GoTo target to <see cref="DialogRuntime.TryGoTo"/>.</summary>
        void SelectVariant(Action.Impls.SelectVariant variant);
        /// <summary>Spawns and displays answer choice buttons in the dialog box.</summary>
        void SetVariants(System.Collections.Generic.ICollection<Action.Impls.SelectVariant> variants);

        /// <summary>Sets the main dialog text. Body visibility toggles based on whether text is empty.</summary>
        void SetText(string text);
        /// <summary>Sets the title bar text. Null or empty hides the title bar.</summary>
        void SetTitle(string title);
        /// <summary>Sets the left (primary) avatar texture. Use <paramref name="hideColor"/> for shader-based silhouette mode. Null hides the avatar.</summary>
        void SetFirstAvatar(Texture2D avatar, bool hideColor = false);
        /// <summary>Sets the right (secondary) avatar texture. Use <paramref name="hideColor"/> for shader-based silhouette mode. Null hides the avatar.</summary>
        void SetSecondAvatar(Texture2D avatar, bool hideColor = false);

        /// <summary>Loads a dialog queue and starts execution at <paramref name="startIndex"/>. Validates that no existing dialog locker is active.</summary>
        void SetDialogQueueAndRun(DialogQueue dialog, System.Collections.Generic.IEnumerable<IActionCommand> endDialogQueue, int startIndex, ExecutableEvent locker);
        /// <summary>Advances past the current action if its wait condition is satisfied. Called by click/key input and update polling.</summary>
        void NextAction();
    }
}
