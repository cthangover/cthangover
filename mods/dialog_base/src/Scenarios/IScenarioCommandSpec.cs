using System;
using System.Collections.Generic;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Optional declarative contract of a scenario command, implemented by
    /// <see cref="IScenarioCommandStrategy"/> classes that want their arguments
    /// checked without the checker knowing the command name. The default members
    /// mean a strategy overrides only what differs from "accepts anything".
    ///
    /// The contract belongs to the command itself, so including a new command
    /// from a mod (or Core) automatically teaches the validator its rules; a
    /// command that does not implement this interface is still recognised as a
    /// known command but its arguments are not constrained.
    /// </summary>
    public interface IScenarioCommandSpec
    {
        /// <summary>Minimum number of positional arguments. Default: 0.</summary>
        int MinPositional => 0;

        /// <summary>Maximum number of positional arguments; -1 means unlimited. Default: -1.</summary>
        int MaxPositional => -1;

        /// <summary>
        /// Minimum number of arguments in total (positional + named). Used by
        /// commands whose required input may arrive either way, e.g. text that
        /// is either literal or a <c>key=</c> lookup.
        /// </summary>
        int MinArguments => 0;

        /// <summary>Named arguments that must be present. Default: none.</summary>
        IReadOnlyList<string> RequiredNamed => Array.Empty<string>();

        /// <summary>
        /// Named arguments the command understands. Empty/null means the command
        /// accepts arbitrary named arguments (e.g. <c>action</c> forwards them as
        /// dialog variables), so unknown-name checks are skipped.
        /// </summary>
        IReadOnlyList<string> AllowedNamed => null;

        /// <summary>
        /// Named argument that carries a <c>:label</c> jump target, or null.
        /// Lets the checker validate that the target exists without knowing the
        /// command; <c>goto</c> declares <c>"target"</c> here.
        /// </summary>
        string JumpTargetNamedKey => null;
    }
}
