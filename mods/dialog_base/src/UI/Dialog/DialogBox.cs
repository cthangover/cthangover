using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Dialog.Action;
using Cthangover.Mods.Dialog.Action.Impls;
using Cthangover.Mods.Dialog.Executable;
using Cthangover.Core.Utils;
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
    /// The visible dialog UI: dual avatars with shader-based hide-color mode,
    /// answer box spawning, and click/keyboard advance. Validates that no existing
    /// dialog locker is running before accepting a new queue, ensuring only one
    /// dialog can occupy the box at a time. Integrates with the audio
    /// system for skip sounds on mouse click. Uses the same Widget lifecycle but
    /// overrides HideDestruct to reset avatar textures and release the locker.
    /// The shader material for avatars is resolved through the mod system on _Ready,
    /// supporting mod-provided avatar effects.
    /// </summary>
	public partial class DialogBox : Widget, IDialogBox
	{

		[Export] private PackedScene answerBoxScene;
		[Export] private Label textField;
		[Export] private Label titleField;
		[Export] private Control titleBody;
		[Export] private TextureRect firstAvatar;
		[Export] private TextureRect secondAvatar;
		[Export] private Texture2D emptyAvatar;

		private IAudioService audioService;
		private AnswerBox answerBox;
		private SceneTreeTimer waitTimer;

		/// <summary>Resolves the body, text and title labels, avatars and the audio service, and applies the mod-provided avatar shader.</summary>
		protected override void OnReady()
		{
			MouseFilter = MouseFilterEnum.Ignore;

			if (Body == null)
			{
				var found = FindChild("DialogBody", true, false);
				if (found != null)
					Set("body", found);
			}
			audioService = GetNodeOrNull<Node>("/root/AudioService") as IAudioService;
			textField ??= GetNode<Label>("DialogBody/TextField");
			titleField ??= GetNode<Label>("TitleBody/TitleField");
			titleBody ??= GetNode<Control>("TitleBody");
			firstAvatar ??= GetNode<TextureRect>("FirstAvatar");
			secondAvatar ??= GetNode<TextureRect>("SecondAvatar");

			var avatarShader = Shaders.Resolve("avatar");
			if (avatarShader != null)
			{
				if (firstAvatar != null && firstAvatar.Material == null)
					firstAvatar.Material = new ShaderMaterial { Shader = avatarShader };
				if (secondAvatar != null && secondAvatar.Material == null)
					secondAvatar.Material = new ShaderMaterial { Shader = avatarShader };
			}
		}

        /// <summary>Mutable lock object that prevents concurrent dialogs. Set when a queue starts, cleared when the dialog ends.</summary>
        public ExecutableEvent Locker { get; set; }
        /// <summary>The execution engine. Created once at construction and reused for all dialog queues.</summary>
        public DialogRuntime Runtime { get; } = new();
        /// <summary>Whether the answer/choice box is currently visible.</summary>
        public bool IsAnswerBoxShowed => answerBox != null;
        /// <summary>If false, new dialog queues are rejected. Set externally to prevent dialog during non-dialog scenes.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Called by answer items when the player picks a choice. Hides the answer box and jumps the runtime to the variant's GoTo target.</summary>
        public void SelectVariant(SelectVariant variant)
		{
			if (answerBox != null)
			{
				answerBox.Hide();
				answerBox = null;
			}

			Runtime.TryGoTo(variant.GoTo);
			Runtime.Run();
			ScheduleWaitTimer();
		}

        /// <summary>Sets the title bar text. Null or empty hides the title bar.</summary>
        public void SetTitle(string title)
		{
			if (title == null) title = string.Empty;
			titleField.Text = title;
			titleBody.Visible = title != string.Empty;
		}

        /// <summary>Instantiates the answer_box scene prefab (bound in dialog_box.tscn), adds it as a child, and populates it with the given choice variants.</summary>
        public void SetVariants(ICollection<SelectVariant> variants)
		{
			if (answerBoxScene == null)
			{
				GameLogger.Log("DIALOG", "SetVariants: answerBoxScene is not bound in the dialog_box scene", LogLevel.Error);
				return;
			}

			var box = answerBoxScene.Instantiate<AnswerBox>();
			AddChild(box);
			answerBox = box;
			answerBox.CreateVariantsUI(variants);
		}

        /// <summary>Sets the main dialog text. Hides the Body canvas item when text is null or whitespace.</summary>
        public void SetText(string text)
		{
			if (text == null) text = string.Empty;
			textField.Text = text;
			var body = Body as CanvasItem;
			if (body != null)
				body.Visible = !string.IsNullOrWhiteSpace(text);
		}

        /// <summary>Sets the left avatar texture. Null hides the slot. <paramref name="hideColor"/> enables shader silhouette mode when true.</summary>
        public void SetFirstAvatar(Texture2D avatar, bool hideColor = false)
		{
			if (avatar == null)
			{
				firstAvatar.Visible = false;
			}
			else
			{
				firstAvatar.Texture = avatar;
				firstAvatar.Visible = true;
				if (firstAvatar.Material is ShaderMaterial mat)
					mat.SetShaderParameter("hide_mode", hideColor ? 1 : 0);
			}
		}

        /// <summary>Sets the right avatar texture. Null hides the slot. <paramref name="hideColor"/> enables shader silhouette mode when true.</summary>
        public void SetSecondAvatar(Texture2D avatar, bool hideColor = false)
		{
			if (avatar == null)
			{
				secondAvatar.Visible = false;
			}
			else
			{
				secondAvatar.Texture = avatar;
				secondAvatar.Visible = true;
				if (secondAvatar.Material is ShaderMaterial mat)
					mat.SetShaderParameter("hide_mode", hideColor ? 1 : 0);
			}
		}

        /// <summary>Resets avatars to empty and releases the dialog locker, preparing for the next dialog session.</summary>
        protected override void HideDestruct()
		{
			ClearWaitTimer();
			firstAvatar.Texture = emptyAvatar;
			firstAvatar.Visible = false;
			secondAvatar.Texture = emptyAvatar;
			secondAvatar.Visible = false;
			Locker = null;
		}

        /// <summary>
        /// Loads and starts a dialog sequence. Validates that: (1) dialog is not null, (2) this DialogBox is active,
        /// (3) no existing dialog locker is running. On success, shows the box and delegates to <see cref="DialogRuntime.SetDialogQueueAndRun"/>.
        /// </summary>
        public void SetDialogQueueAndRun(DialogQueue dialog, IEnumerable<IActionCommand> endDialogQueue, int startIndex, ExecutableEvent locker)
		{
			if (dialog == null)
			{
				GameLogger.Log("DIALOG", $"can't run '{locker?.GetType().FullName}', dialog is null", LogLevel.Error);
				return;
			}
			if (!IsActive)
			{
				GameLogger.Log("DIALOG", $"can't run '{locker?.GetType().FullName}', DialogBox is not active", LogLevel.Error);
				return;
			}

			if (Locker != null)
			{
				GameLogger.Log("DIALOG", $"can't run '{locker?.GetType().FullName}', locker already executing!", LogLevel.Error);
				return;
			}

			GameLogger.Log("DIALOG", $"SetDialogQueueAndRun: locker='{locker?.GetType().Name}', queue size={dialog?.Queue.Count}");
			Locker = locker;
			Show();
			Runtime.SetDialogQueueAndRun(this, dialog.Queue, endDialogQueue, startIndex);
			Runtime.Dialog = dialog;
			Runtime.Run();
			ScheduleWaitTimer();
		}

        /// <summary>
        /// Advances the dialog to the next action. Skips if the runtime is waiting for an answer or has ended.
        /// Respects <see cref="WaitType.WaitEvent"/> (waits for destruct) and <see cref="WaitType.WaitTime"/> (waits for elapsed duration).
        /// Called by click/keyboard input, the wait timer, and self-completing WaitEvent actions.
        /// </summary>
        public void NextAction()
		{
			if (Runtime.IsHeld || Runtime.IsWaitAnswer)
				return;
			if (Runtime.IsEnd)
			{
				GameLogger.Log("DIALOG", "NextAction: dialog ended, hiding");
				Hide();
				return;
			}

			var action = Runtime.CurrentAction;
			if (action == null)
			{
				GameLogger.Log("DIALOG", "NextAction: no current action, hiding");
				Hide();
				return;
			}

			double now = Godot.Time.GetTicksUsec() / 1_000_000.0;
			if (action.WaitType == WaitType.WaitEvent && !action.IsDestructed)
				return;
			if (action.WaitType == WaitType.WaitTime && now - action.StartTime < action.WaitTime)
				return;
			
			GameLogger.Log("DIALOG", $"NextAction: advancing past '{action.GetType().Name}' (idx={Runtime.CurrentAction?.GetType().Name})");

			Runtime.Next();
			Runtime.Run();
			ScheduleWaitTimer();
		}

		/// <summary>Advances the dialog on <c>ui_accept</c> or on a click outside the body, unless a choice is pending or the queue has ended.</summary>
		protected override void OnInput(InputEvent @event)
		{
			if (Runtime.IsHeld || Runtime.IsEnd || Runtime.IsWaitAnswer)
				return;

			if (@event.IsActionPressed("ui_accept"))
			{
				if (IsTyping())
					return;
				GameLogger.Log("DIALOG", "input: advancing dialog");
				NextAction();
			}
			else if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left && btn.Pressed)
			{
				var action = Runtime.CurrentAction;
				if (action == null || action.WaitType == WaitType.WaitTime || action.WaitType == WaitType.WaitEvent)
					return;
				
				if (IsOverOtherControl())
					return;

				if (Body is Control bodyControl)
				{
					var bodyRect = bodyControl.GetGlobalRect();
					if (bodyRect.HasPoint(btn.Position))
					{
						GameLogger.Log("DIALOG", "input: advancing dialog");
						audioService.PlaySound("ui/skip", SoundType.UI);
						NextAction();
					}
				}
			}
        }

        /// <summary>
        /// True while a text field has keyboard focus: Enter and Space belong to
        /// it, not to the dialog (the dialog reads <c>ui_accept</c> in
        /// <c>_Input</c>, before the GUI).
        /// </summary>
        private bool IsTyping() => GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit;

        /// <summary>
        /// True when the mouse is over a control that is not part of this box and
        /// stops the mouse (a tool panel, a button drawn over the dialog area):
        /// the click is meant for that control.
        /// </summary>
        private bool IsOverOtherControl()
        {
            var hovered = GetViewport()?.GuiGetHoveredControl();
            return hovered != null && hovered != this && !IsAncestorOf(hovered)
                && hovered.MouseFilter == MouseFilterEnum.Stop;
        }

        /// <summary>
        /// Continues a dialog the debugger held (<see cref="DialogRuntime.IsHeld"/>):
        /// runs from the held action on, exactly as if it had never stopped.
        /// Does nothing when the runtime is not held.
        /// </summary>
        public void Resume()
        {
            if (!Runtime.IsHeld)
                return;

            Runtime.IsHeld = false;
            Runtime.Run();
            ScheduleWaitTimer();
        }

        /// <summary>
        /// Moves a running dialog to another action and holds it there
        /// (<see cref="DialogRuntime.MoveTo"/>): a pending wait timer and an open
        /// answer box are dropped, since they belong to the action left behind;
        /// the shown text stays until the new action replaces it.
        /// </summary>
        public bool MoveTo(int queueIndex)
        {
            if (!IsRunning)
                return false;

            ClearWaitTimer();
            if (answerBox != null)
            {
                answerBox.Hide();
                answerBox = null;
            }
            return Runtime.MoveTo(queueIndex);
        }

        /// <summary>
        /// Stops the running dialog without finishing it (<see cref="DialogRuntime.Abort"/>)
        /// and frees the box for another queue: closes the answer box, hides
        /// and releases the locker. The scenario does not count as completed.
        /// </summary>
        public void Abort()
        {
            ClearWaitTimer();
            if (answerBox != null)
            {
                answerBox.Hide();
                answerBox = null;
            }

            Runtime.Abort();
            Hide();
            Locker = null;
        }

        /// <summary>Whether a dialog is currently running (Locker is not null).</summary>
        public bool IsRunning => Locker != null;

        /// <summary>Whether the current dialog queue reached its end (also true before the first queue loads).</summary>
        public bool IsDialogEnded => Runtime.IsEnd;

        /// <summary>Whether the dialog is waiting for the player to pick a variant.</summary>
        public bool IsWaitingAnswer => Runtime.IsWaitAnswer;

        /// <summary>Number of answer variants currently shown (0 when the answer box is hidden).</summary>
        public int AnswerVariantCount => answerBox?.CreateModels()?.Count ?? 0;

        /// <summary>Selects the shown variant at <paramref name="index"/>. Returns false when nothing is shown or the index is out of range.</summary>
        public bool TrySelectVariant(int index)
        {
            var models = answerBox?.CreateModels();
            if (models == null || models.Count == 0 || index < 0 || index >= models.Count)
                return false;

            var list = new List<SelectVariant>(models);
            SelectVariant(list[index]);
            return true;
        }

        /// <summary>
        /// Schedules a one-shot <see cref="SceneTreeTimer"/> that advances the dialog past the
        /// current <see cref="WaitType.WaitTime"/> action once its <see cref="ActionCommand.WaitTime"/>
        /// elapses. Replaces the former per-frame polling: the runtime now yields until the timer
        /// fires exactly once, so no Update work happens between delays.
        /// </summary>
        private void ScheduleWaitTimer()
        {
            ClearWaitTimer();

            if (!Runtime.IsWaitTime)
                return;

            var wait = Runtime.CurrentAction?.WaitTime ?? 0f;
            if (wait <= 0f)
            {
                NextAction();
                return;
            }

            waitTimer = GetTree().CreateTimer(wait);
            waitTimer.Timeout += OnWaitTimerTimeout;
        }

        /// <summary>
        /// Fires when the delay timer elapses. The timer already guarantees the wait has
        /// elapsed, so the runtime is advanced directly — no wall-clock re-check. The
        /// <see cref="Runtime.IsWaitTime"/> guard skips a stale timer that fired after the
        /// dialog already moved on.
        /// </summary>
        private void OnWaitTimerTimeout()
        {
            waitTimer = null;

            if (!Runtime.IsWaitTime)
                return;

            Runtime.Next();
            Runtime.Run();
            ScheduleWaitTimer();
        }

        /// <summary>
        /// Cancels a pending delay timer. Called before scheduling a new one and when the dialog
        /// hides, so a stale timer can never advance a later, unrelated dialog session.
        /// </summary>
        private void ClearWaitTimer()
        {
            if (waitTimer == null)
                return;

            waitTimer.Timeout -= OnWaitTimerTimeout;
            waitTimer = null;
        }
	}
}
