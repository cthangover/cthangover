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

		public ExecutableEvent Locker { get; set; }
		public DialogRuntime Runtime { get; } = new();
		public bool IsAnswerBoxShowed => answerBox != null;
		public bool IsActive { get; set; } = true;

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

		public void SetTitle(string title)
		{
			if (title == null) title = string.Empty;
			titleField.Text = title;
			titleBody.Visible = title != string.Empty;
		}

		public void SetVariants(ICollection<SelectVariant> variants)
		{
			var scene = answerBoxScene.Instantiate<AnswerBox>();
			AddChild(scene);
			answerBox = scene;
			answerBox.CreateVariantsUI(variants);
		}

		public void SetText(string text)
		{
			if (text == null) text = string.Empty;
			textField.Text = text;
			var body = Body as CanvasItem;
			if (body != null)
				body.Visible = !string.IsNullOrWhiteSpace(text);
		}

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

		protected override void HideDestruct()
		{
			firstAvatar.Texture = emptyAvatar;
			firstAvatar.Visible = false;
			secondAvatar.Texture = emptyAvatar;
			secondAvatar.Visible = false;
			Locker = null;
		}

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

		public int Priority => 1;
		public bool IsRunning => Locker != null;

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
