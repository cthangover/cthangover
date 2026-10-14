using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Dialog box contract retained in the core as the transition surface for
    /// scene-level consumers. The concrete DialogBox implementation and the
    /// extended runtime surface (queue execution, variant selection, runtime
    /// inspection) live in the dialog_base engine mod; core code (SceneManager,
    /// InteractiveManager) only activates, deactivates and inspects the box
    /// through this contract.
    /// </summary>
    public interface IDialogBox : IWidget
    {
        /// <summary>Whether the answer/choice box is currently displayed.</summary>
        bool IsAnswerBoxShowed { get; }

        /// <summary>Whether a dialog queue is currently executing (locker present).</summary>
        bool IsRunning { get; }

        /// <summary>Gate for new dialog queues. Set to false to prevent dialogs during non-dialog scenes.</summary>
        bool IsActive { get; set; }

        /// <summary>Sets the main dialog text. Body visibility toggles based on whether text is empty.</summary>
        void SetText(string text);
        /// <summary>Sets the title bar text. Null or empty hides the title bar.</summary>
        void SetTitle(string title);
        /// <summary>Sets the left (primary) avatar texture. Use <paramref name="hideColor"/> for shader-based silhouette mode. Null hides the avatar.</summary>
        void SetFirstAvatar(Texture2D avatar, bool hideColor = false);
        /// <summary>Sets the right (secondary) avatar texture. Use <paramref name="hideColor"/> for shader-based silhouette mode. Null hides the avatar.</summary>
        void SetSecondAvatar(Texture2D avatar, bool hideColor = false);

        /// <summary>Advances past the current action if its wait condition is satisfied. Called by click/key input and update polling.</summary>
        void NextAction();

        /// <summary>Whether the current dialog queue reached its end (also true before the first queue loads).</summary>
        bool IsDialogEnded { get; }

        /// <summary>Whether the dialog is waiting for the player to pick a variant.</summary>
        bool IsWaitingAnswer { get; }

        /// <summary>Number of answer variants currently shown (0 when the answer box is hidden).</summary>
        int AnswerVariantCount { get; }

        /// <summary>Selects the shown answer variant at <paramref name="index"/>. Returns false when no variants are shown or the index is out of range.</summary>
        bool TrySelectVariant(int index);
    }
}