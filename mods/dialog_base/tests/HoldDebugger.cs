namespace Cthangover.Mods.Dialog.Tests
{
    using Cthangover.Mods.Dialog.Action;

    /// <summary>
    /// Debugger stand-in that holds the runtime once, right before
    /// <see cref="Target"/> — what the scenario studio does on a breakpoint.
    /// </summary>
    public class HoldDebugger : IDialogDebugger
    {
        private bool held;

        /// <summary>The action to hold before.</summary>
        public IActionCommand Target { get; set; }

        /// <summary>Holds before <see cref="Target"/> the first time it is reached.</summary>
        public bool ShouldHold(DialogRuntime runtime, IActionCommand action)
        {
            if (held || action != Target)
                return false;
            held = true;
            return true;
        }
    }
}
