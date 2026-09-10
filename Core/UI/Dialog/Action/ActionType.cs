namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// High-level action category discriminator. Currently minimal (Text, End)
    /// — may expand as additional action families are added.
    /// </summary>
    public enum ActionType
    {
        /// <summary>A dialog text display action that pauses for click input.</summary>
        Text,
        /// <summary>Terminates the dialog queue.</summary>
        End
    }
}
