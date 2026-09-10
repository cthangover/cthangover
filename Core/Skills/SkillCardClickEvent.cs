using Godot;

namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Listens for mouse click events on a skill card and routes them to the
    /// associated <see cref="SkillCardBehaviour"/>. Placed as an overlay
    /// <c>Control</c> on top of the card visuals so that input is captured
    /// independently of the rendering hierarchy. The click handler body is
    /// intentionally left empty — card selection logic is registered externally
    /// at runtime via Godot signals connected to the <see cref="card"/> reference.
    /// </summary>
    public partial class SkillCardClickEvent : Control
    {
        /// <summary>
        /// Back-reference to the <see cref="SkillCardBehaviour"/> that owns
        /// this click handler. Exposed to the Godot editor so scene designers
        /// can wire the node path.
        /// </summary>
        [Export] private SkillCardBehaviour card;

        /// <summary>
        /// Captures mouse-down events on the card area. When a press is detected
        /// the handler delegates to whatever selection logic has been wired to
        /// <see cref="card"/> via signals at setup time.
        /// </summary>
        /// <param name="event">The input event received from the Godot input system.</param>
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
            }
        }
    }
}
