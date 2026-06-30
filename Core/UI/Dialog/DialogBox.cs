using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// The visible dialog UI: dual avatars with shader-based hide-color mode,
    /// answer box spawning, and click/keyboard advance. Validates that no existing
    /// dialog locker is running before accepting a new queue, ensuring only one
    /// dialog can occupy the box at a time. OnUpdate is throttle-gated (100ms min
    /// interval) to avoid CPU waste on the advance check. Integrates with the audio
    /// system for skip sounds on mouse click. Uses the same Widget lifecycle but
    /// overrides HideDestruct to reset avatar textures and release the locker.
    /// The shader material for avatars is resolved through the mod system on _Ready,
    /// supporting mod-provided avatar effects.
    /// </summary>
	public partial class DialogBox : Widget, IDialogBox, IOnUpdateEvent
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
		private float lastTimestamp;

		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Ignore;

			if (Body == null)
			{
				var found = FindChild("DialogBody", true, false);
				if (found != null)
					Set("body", found);
			}
			audioService = GetNode<AudioService>("/root/AudioService");
			textField ??= GetNode<Label>("DialogBody/TextField");
			titleField ??= GetNode<Label>("TitleBody/TitleField");
			titleBody ??= GetNode<Control>("TitleBody");
			firstAvatar ??= GetNode<TextureRect>("FirstAvatar");
			secondAvatar ??= GetNode<TextureRect>("SecondAvatar");

			var avatarShader = ModManager.Instance.ResolveShader("avatar");
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
		}

        /// <summary>Sets the title bar text. Null or empty hides the title bar.</summary>
        public void SetTitle(string title)
		{
			if (title == null) title = string.Empty;
			titleField.Text = title;
			titleBody.Visible = title != string.Empty;
		}

        /// <summary>Instantiates the AnswerBox scene, adds it as a child, and populates it with the given choice variants.</summary>
        public void SetVariants(ICollection<SelectVariant> variants)
		{
			var scene = answerBoxScene.Instantiate<AnswerBox>();
			AddChild(scene);
			answerBox = scene;
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
		}

        /// <summary>
        /// Advances the dialog to the next action. Skips if the runtime is waiting for an answer or has ended.
        /// Respects <see cref="WaitType.WaitEvent"/> (waits for destruct) and <see cref="WaitType.WaitTime"/> (waits for elapsed duration).
        /// Called by click/keyboard input and by <see cref="OnUpdate"/> polling.
        /// </summary>
        public void NextAction()
		{
			if (Runtime.IsWaitAnswer)
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

			double now = Time.GetTicksUsec() / 1_000_000.0;
			if (action.WaitType == WaitType.WaitEvent && !action.IsDestructed)
				return;
			if (action.WaitType == WaitType.WaitTime && now - action.StartTime < action.WaitTime)
				return;
			
			GameLogger.Log("DIALOG", $"NextAction: advancing past '{action.GetType().Name}' (idx={Runtime.CurrentAction?.GetType().Name})");

			Runtime.Next();
			Runtime.Run();
		}

		public override void _Input(InputEvent @event)
		{
			if (Runtime.IsEnd || Runtime.IsWaitAnswer)
				return;

			if (@event.IsActionPressed("ui_accept"))
			{
				GameLogger.Log("DIALOG", "input: advancing dialog");
				NextAction();
			}
			else if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left && btn.Pressed)
			{
				var action = Runtime.CurrentAction;
				if (action == null || action.WaitType == WaitType.WaitTime || action.WaitType == WaitType.WaitEvent)
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

        /// <summary>Whether a dialog is currently running (Locker is not null).</summary>
        public bool IsRunning => Locker != null;

        /// <summary>Update priority. Default 1.</summary>
        public int Priority => 1;
        /// <summary>
        /// Polling loop for time/event-waiting actions. Throttled to 100ms minimum interval.
        /// If the current action is a WaitEvent type and its destruct flag is set, advances.
        /// If the current action is a WaitTime type and elapsed >= WaitTime, advances.
        /// </summary>
        public void OnUpdate()
		{
			double now = Time.GetTicksUsec() / 1_000_000.0;
			var delta = now - lastTimestamp;
			if (delta < 0.1)
				return;
			lastTimestamp = (float)now;

			if (Runtime.IsEnd)
				return;

			bool waitTime = Runtime.IsWaitTime;
			bool waitEvent = Runtime.IsWaitEvent;
			if (!waitTime && !waitEvent)
				return;

			var action = Runtime.CurrentAction;
			if (waitEvent && action?.IsDestructed == true)
				NextAction();
			if (waitTime && now - action?.StartTime >= action?.WaitTime)
				NextAction();
		}
	}
}
