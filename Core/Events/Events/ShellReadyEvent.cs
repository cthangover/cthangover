using Godot;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by the base shell (<c>ExecutableMainEventChain._Ready</c>) once the
    /// in-game UI shell is instantiated and its children are ready. <c>UiHost</c>
    /// uses it as the single "build the mod GUI now" trigger: it attaches cached
    /// panel instances to the new shell and <c>UiBuilder</c> creates the toolbar
    /// buttons. There is deliberately no teardown counterpart — panels live in the
    /// UiHost autoload and survive shell changes such as entering battle.
    /// </summary>
    [LifecycleEvent]
    [EventKey("shell_ready")]
    public class ShellReadyEvent : ModEvent
    {
        /// <summary>Root node of the freshly created shell (the base scene root).</summary>
        public Node Root { get; init; }
    }
}
