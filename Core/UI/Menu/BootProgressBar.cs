using Cthangover.Core.Bootstrap;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Thin progress bar for the launcher boot overlay. Polls the thread-safe
    /// <see cref="BootProgress"/> snapshot every frame — the boot pipeline and
    /// the mod compiler run on worker threads and never touch Godot nodes, so
    /// all rendering-side updates happen here in <see cref="_Process"/>.
    /// Custom-drawn instead of a themed ProgressBar to keep the boot overlay's
    /// minimal look: a flat accent strip over a faint well.
    /// </summary>
    public partial class BootProgressBar : Godot.Control
    {
        /// <summary>Displayed value chases the snapshot with this smoothing factor per second.</summary>
        private const float EasePerSecond = 6f;

        private float _displayed;

        /// <inheritdoc/>
        public BootProgressBar()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new(0, 10);
        }

        /// <summary>Repaints the bar from the latest <see cref="BootProgress.GetOverall"/> snapshot.</summary>
        public override void _Process(double delta)
        {
            var target = BootProgress.GetOverall();
            // Ease toward the target; never ease backwards so a fast phase
            // transition cannot visually rewind the bar.
            if (target > _displayed)
                _displayed = System.Math.Min(target, _displayed + EasePerSecond * (float)delta * (target - _displayed + 0.02f));
            else
                _displayed = target;
            QueueRedraw();
        }

        /// <summary>Draws the well and the accent fill for the current progress value.</summary>
        public override void _Draw()
        {
            var size = GetSize();
            DrawRect(new Rect2(0, 0, size.X, size.Y), UiTokens.Well);
            DrawRect(new Rect2(0, 0, size.X, 1), UiTokens.LineSoft);
            DrawRect(new Rect2(0, size.Y - 1, size.X, 1), UiTokens.LineSoft);
            if (_displayed <= 0f)
                return;
            var fillWidth = size.X * _displayed;
            DrawRect(new Rect2(0, 0, fillWidth, size.Y), UiTokens.Accent);
            // Bright leading edge so movement stays readable during long phases.
            DrawRect(new Rect2(fillWidth - 2, 0, 2, size.Y), UiTokens.AccentBright);
        }
    }
}
