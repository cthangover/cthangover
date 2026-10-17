namespace Cthangover.Core.Mods.Pipeline
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// Outcome of an in-process mod-set reload. <see cref="Applied"/> is false
    /// when the preparation failed (compile errors) or another reload was
    /// running — the running set is then untouched. <see cref="Leaked"/> is
    /// filled by the asynchronous verification: mods whose old context
    /// survived collection (a reference nobody released; the log of the
    /// unload names the candidates).
    /// </summary>
    public sealed class ModReloadResult
    {
        internal ModReloadResult(bool applied, ModReloadPlan plan, ModUnloadTicket ticket, IReadOnlyList<string> errors)
        {
            Applied = applied;
            Plan = plan;
            Ticket = ticket ?? new ModUnloadTicket(null);
            Errors = errors ?? new List<string>();
        }

        /// <summary>True when the process now runs the new set; false leaves the previous set untouched.</summary>
        public bool Applied { get; }

        /// <summary>What happened to each mod with code; null when nothing was applied.</summary>
        public ModReloadPlan Plan { get; }

        /// <summary>Weak references to the released contexts; verification turns it into <see cref="Leaked"/>.</summary>
        public ModUnloadTicket Ticket { get; }

        /// <summary>Why the reload was rejected (compile errors, a reload already running); empty when applied.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>Mods whose released context is still alive after verification.</summary>
        public IReadOnlyList<string> Leaked { get; internal set; } = new List<string>();
    }
}
