namespace Cthangover.Core.Interactive
{
    /// <summary>
    /// Dialog-side executor for interactive click/hover DSL commands. Implemented
    /// by the dialog_base engine mod (which owns DialogBox, DialogRuntime and the
    /// scenario parser); InteractiveManager resolves the implementation from the
    /// well-known /root/DialogRunner node without referencing mod types. Takes the
    /// interactive definition id as a string so this contract stays free of the
    /// definition DTO, which lives in the interactive_base engine mod.
    /// </summary>
    public interface IInteractiveDialogRunner
    {
        /// <summary>
        /// Builds and runs the click dialog for the interactive definition with the
        /// given id. Returns false when the definition is unknown, carries no
        /// runnable commands, or the dialog box is missing/busy.
        /// </summary>
        bool TryExecuteClickDialog(string definitionId);

        /// <summary>Runs inline DSL commands (hover effects and similar) through a standalone dialog runtime.</summary>
        void ExecuteInlineCommands(string dsl);
    }
}