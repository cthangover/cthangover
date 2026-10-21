using Cthangover.Core.Scenes;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Sets a story flag of the running game (<see cref="ScenarioCondition.SetFlag"/>).
    /// Unlike <see cref="ActionSet"/>, whose variables end with the dialog, a
    /// flag is game state: it is saved, read by scenario conditions
    /// (<c>condition: door == "open"</c>) and its change rescans the scene's
    /// event chain, so a scenario waiting for it can start right after.
    /// </summary>
    public class ActionSetFlag : ActionCommand
    {
        /// <summary>Flag name, as used in conditions.</summary>
        public string Name { get; set; }
        /// <summary>Flag value; numeric values compare as integers in conditions.</summary>
        public string Value { get; set; }

        /// <summary>Setting a flag is instant — the dialog continues without pause.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <inheritdoc/>
        public override void DoRun(DialogRuntime runtime) => ScenarioCondition.SetFlag(Name, Value ?? string.Empty);
    }
}
