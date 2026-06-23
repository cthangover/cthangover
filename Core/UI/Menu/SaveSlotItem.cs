using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    public partial class SaveSlotItem : ListItem<SaveSlotInfo>
    {
        [Export] private TextureRect screenshotRect;
        [Export] private Label dateLabel;
        [Export] private Label timeLabel;
        [Export] private Label sceneLabel;
        [Export] private Label emptyLabel;
        [Export] private Button slotButton;

        [Signal]
        public delegate void SlotPressedEventHandler(string fileName);

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
                emptyLabel.Text = TranslationServer.Translate("ui/save/empty");
            }
            else
            {
                emptyLabel.Visible = false;
                screenshotRect.Visible = true;
                dateLabel.Visible = true;
                timeLabel.Visible = true;
                sceneLabel.Visible = true;

                var tex = SaveScreenshotService.LoadScreenshot(model.FileName);
                if (tex != null)
                    screenshotRect.Texture = tex;

                dateLabel.Text = model.SaveTime != System.DateTime.MinValue
                    ? model.SaveTime.ToLocalTime().ToString("d MMM yyyy")
                    : "";

                timeLabel.Text = model.SaveTime != System.DateTime.MinValue
                    ? model.SaveTime.ToLocalTime().ToString("HH:mm")
                    : "";

                sceneLabel.Text = model.SceneName;
            }

            slotButton.Pressed += OnPressed;
        }

        private void OnPressed()
        {
            EmitSignal(SignalName.SlotPressed, Model.FileName);
        }

        public override void Destruct()
        {
            if (slotButton != null && GodotObject.IsInstanceValid(slotButton))
                slotButton.Pressed -= OnPressed;
            QueueFree();
        }
    }
}
