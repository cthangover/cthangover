using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Validator
{
    /// <summary>
    /// Shared fact base of a validation pass. Contract strategies append to it
    /// and reconciliation strategies query it, which keeps cross-cutting checks
    /// (missing references, type conflicts, unused declarations) independent of
    /// the parser that produced the facts. A scenario strategy added later only
    /// needs to call <see cref="AddDeclaration"/> and <see cref="AddReference"/>.
    ///
    /// The graph is filled and read sequentially by one background thread, so it
    /// needs no synchronisation; it is never exposed to the main thread.
    /// </summary>
    public class ValidationGraph
    {
        private readonly Dictionary<string, List<ValidationDeclaration>> declarations = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ValidationReference> references = new();
        private readonly HashSet<string> declaredKeys = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>All references collected so far.</summary>
        public IReadOnlyList<ValidationReference> References => references;

        /// <summary>All declarations collected so far, flattened across kinds.</summary>
        public IEnumerable<ValidationDeclaration> Declarations => declarations.Values.SelectMany(list => list);

        /// <summary>
        /// Registers an entity declaration. Empty kinds/ids are ignored so
        /// parsers can call this without nil-checks.
        /// </summary>
        public void AddDeclaration(string kind, string id, string modId, string file, int line)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
                return;

            if (!declarations.TryGetValue(kind, out var list))
            {
                list = new List<ValidationDeclaration>();
                declarations[kind] = list;
            }

            list.Add(new ValidationDeclaration
            {
                Kind = kind,
                Id = id,
                ModId = modId,
                File = file,
                Line = line,
            });
        }

        /// <summary>
        /// Registers a reference to an entity. Empty kinds/targets are ignored.
        /// </summary>
        public void AddReference(string kind, string targetId, string modId, string file, int line)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(targetId))
                return;

            references.Add(new ValidationReference
            {
                Kind = kind,
                TargetId = targetId,
                ModId = modId,
                File = file,
                Line = line,
            });
        }

        /// <summary>
        /// Registers a property key seen in JSON entity data. Used by the C#
        /// strategy to distinguish declared properties from typos.
        /// </summary>
        public void AddDeclaredKey(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                declaredKeys.Add(key);
        }

        /// <summary>True when at least one declaration of the given kind and id exists.</summary>
        public bool HasDeclaration(string kind, string id)
        {
            return declarations.TryGetValue(kind, out var list)
                && list.Any(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True when the key was seen in any JSON entity or action Values list.</summary>
        public bool IsKeyDeclared(string key) => declaredKeys.Contains(key);
    }
}
