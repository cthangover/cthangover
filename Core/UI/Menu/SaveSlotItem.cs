using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.UI.Menu
{
	/// <summary>
	/// A single save slot card in the save/load grid. Extends ListItem with
	/// SaveSlotInfo as the model. Renders differently for empty vs. occupied
	/// slots: empty slots show a placeholder label; occupied slots display the
	/// screenshot, in-game date (from TimeData), real-world save timestamp, and
	/// scene name. The stats label shows party size. Button click emits SlotPressed
	/// with the file name for parent handling. Destruct disconnects the button
	/// signal before QueueFree to avoid phantom callbacks.
	/// </summary>
	public partial class SaveSlotItem : ListItem<SaveSlotInfo>
	{
		private TextureRect screenshotRect;
		private Label dateLabel;
		private Label timeLabel;
		private Label sceneLabel;
		private Label emptyLabel;
		private Button slotButton;
		private Label statsLabel;

        /// <summary>
        /// Emitted when this specific slot is clicked, carrying the save file name so the parent
        /// <see cref="SaveSlotList"/> can forward it to <see cref="SaveLoadMenu"/>.
        /// </summary>
		[Signal]
		public delegate void SlotPressedEventHandler(string fileName);

		public override void _Ready()
		{
			screenshotRect = GetNode<TextureRect>("ScreenshotRect");
			dateLabel = GetNode<Label>("DateLabel");
			timeLabel = GetNode<Label>("TimeLabel");
			sceneLabel = GetNode<Label>("SceneLabel");
			emptyLabel = GetNode<Label>("EmptyLabel");
			slotButton = GetNode<Button>("SlotButton");
			statsLabel = GetNodeOrNull<Label>("StatsLabel");
		}

		/// <summary>
		/// Populates the slot visuals from the <see cref="SaveSlotInfo"/> model. For occupied slots,
		/// loads the screenshot texture, formats the game date from <see cref="SaveSlotInfo.GameTime"/>,
		/// shows the real-world save timestamp, and displays party size. Empty slots show only a
		/// localized "empty" label. Wires the button click signal to emit <see cref="SlotPressed"/>.
		/// </summary>
		public override void Construct(SaveSlotInfo model)
		{
			base.Construct(model);

			if (model.IsEmpty)
			{
				screenshotRect.Visible = false;
				dateLabel.Visible = false;
				timeLabel.Visible = false;
				sceneLabel.Visible = false;
				emptyLabel.Visible = true;
				if (statsLabel != null) statsLabel.Visible = false;
				emptyLabel.Text = TranslationServer.Translate("ui/save/empty");
			}
			else
			{
				emptyLabel.Visible = false;
				screenshotRect.Visible = true;
				dateLabel.Visible = true;
				timeLabel.Visible = true;
				sceneLabel.Visible = true;
				if (statsLabel != null) statsLabel.Visible = true;

				var tex = SaveScreenshotService.LoadScreenshot(model.FileName);
				if (tex != null)
					screenshotRect.Texture = tex;

				var gameTimeObj = new TimeData(model.GameTime);
				dateLabel.Text = TranslationServer.Translate("ui/save/game_day").ToString()
					.Replace("{day}", (gameTimeObj.Days + 1).ToString())
					.Replace("{time}", gameTimeObj.Text);

				timeLabel.Text = model.SaveTime != System.DateTime.MinValue
					? model.SaveTime.ToLocalTime().ToString("dd.MM.yy HH:mm")
					: "";

				sceneLabel.Text = model.SceneName;

				if (statsLabel != null)
					statsLabel.Text = TranslationServer.Translate("ui/save/party_size").ToString()
						.Replace("{count}", model.CharacterCount.ToString());
			}

			slotButton.Pressed += OnPressed;
		}

		private void OnPressed()
		{
			EmitSignal(SignalName.SlotPressed, Model.FileName);
		}

		/// <summary>
		/// Disconnects the button signal to prevent callbacks on freed nodes, then queues
		/// this item for deletion. Called by the <see cref="SaveSlotList"/> when rebuilding
		/// the grid, ensuring no dangling signal references.
		/// </summary>
		public override void Destruct()
		{
			if (slotButton != null && GodotObject.IsInstanceValid(slotButton))
				slotButton.Pressed -= OnPressed;
			QueueFree();
		}
	}
}
