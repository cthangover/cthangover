using System.Collections.Generic;
using System.Threading;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Everything a strategy needs: the mod snapshot, the report to fill, the
    /// shared fact graph and the cancellation token. Instances are created per
    /// pass on the validator background thread; strategies run sequentially, so
    /// no locking is required inside the context or the graph.
    /// </summary>
    public class ValidationContext
    {
        public ValidationContext(
            IReadOnlyList<IModInfo> mods,
            ValidationReport report,
            ValidationGraph graph,
            CancellationToken cancellationToken)
        {
            Mods = mods;
            Report = report;
            Graph = graph;
            CancellationToken = cancellationToken;
        }

        /// <summary>Ordered snapshot of the mods to validate.</summary>
        public IReadOnlyList<IModInfo> Mods { get; }

        /// <summary>Report being assembled; strategies append issues to it.</summary>
        public ValidationReport Report { get; }

        /// <summary>Facts collected by contract strategies.</summary>
        public ValidationGraph Graph { get; }

        /// <summary>Cooperative cancellation checked between mods and documents.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Appends an error issue to the report (validation failure).</summary>
        public void AddError(string code, string message, string mod, string file = null, int line = 0, string suggestion = null, List<string> related = null)
            => Report.Issues.Add(Create(ValidationSeverity.Error, code, message, mod, file, line, suggestion, related));

        /// <summary>Appends a warning issue (suspicious but not fatal).</summary>
        public void AddWarning(string code, string message, string mod, string file = null, int line = 0, string suggestion = null, List<string> related = null)
            => Report.Issues.Add(Create(ValidationSeverity.Warning, code, message, mod, file, line, suggestion, related));

        /// <summary>Appends an informational issue (e.g. unused content).</summary>
        public void AddInfo(string code, string message, string mod, string file = null, int line = 0, string suggestion = null, List<string> related = null)
            => Report.Issues.Add(Create(ValidationSeverity.Info, code, message, mod, file, line, suggestion, related));

        /// <summary>Cooperative cancellation hook for strategies to call between documents.</summary>
        public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

        private static ValidationIssue Create(
            string severity,
            string code,
            string message,
            string mod,
            string file,
            int line,
            string suggestion,
            List<string> related)
        {
            return new ValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                Mod = mod,
                File = file,
                Line = line,
                Suggestion = suggestion,
                Related = related,
            };
        }
    }
}
