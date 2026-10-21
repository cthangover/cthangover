namespace Cthangover.Mods.Dialog.Tests
{
    using Cthangover.Mods.Dialog.Action;
    using Cthangover.Core.UI.Dialog;

    /// <summary>
    /// Test action that only counts its lifecycle calls: how often the runtime ran,
    /// constructed and destructed it. Waits for a click by default, so a test can
    /// park the dialog on it and look at where the runtime stands.
    /// </summary>
    public class ProbeAction : ActionCommand
    {
        /// <summary>Number of <see cref="DoRun"/> calls.</summary>
        public int Runs { get; private set; }

        /// <summary>Number of <see cref="DoConstruct"/> calls.</summary>
        public int Constructs { get; private set; }

        /// <summary>Number of <see cref="DoDestruct"/> calls.</summary>
        public int Destructs { get; private set; }

        /// <summary>Counts the run.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            Runs++;
        }

        /// <summary>Counts the construction.</summary>
        public override void DoConstruct()
        {
            Constructs++;
        }

        /// <summary>Counts the destruction.</summary>
        public override void DoDestruct()
        {
            Destructs++;
        }
    }
}
