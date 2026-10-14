namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Core contract for executable scene events. The concrete
    /// ExecutableEvent implementation lives in the dialog_base engine mod;
    /// core consumers (SceneManager, SaveService, SceneEventRegistry) work
    /// against this interface so they never reference mod types.
    /// </summary>
    public interface IExecutableEvent
    {
        /// <summary>True until the event runs for the first time. Used by chains to decide whether a one-shot event has fired.</summary>
        bool IsFirstRun { get; }

        /// <summary>When true, the event fires exactly once and never again, even across scene revisits.</summary>
        bool IsOneRun { get; set; }

        /// <summary>Stable identifier of the event (type name or scenario id) used for save/load tracking and locker matching.</summary>
        string ID { get; }
    }
}