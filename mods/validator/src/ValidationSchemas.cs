namespace Cthangover.Validator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Cthangover.Mods.Schemas;

    /// <summary>
    /// The schemas of the validated mods (their <c>schemas/*.json</c>, read from the providers
    /// in the context's mod order), built once per validation run and shared by the
    /// strategies that need them: the entity strategy treats a described kind as known, the
    /// schema strategy checks content against it.
    /// </summary>
    public static class ValidationSchemas
    {
        private static readonly ConditionalWeakTable<ValidationContext, SchemaCatalog> Catalogs = new();

        /// <summary>The merged catalog of <paramref name="context"/>'s mods.</summary>
        public static SchemaCatalog Of(ValidationContext context)
            => Catalogs.GetValue(context, Build);

        private static SchemaCatalog Build(ValidationContext context) => SchemaCatalog.Build(Files(context));

        private static IEnumerable<(string, string, string)> Files(ValidationContext context)
        {
            foreach (var mod in context.Mods.Where(m => m.FileProvider != null))
            {
                foreach (var file in ModFileReader.ListFiles(mod.FileProvider, "schemas", true).Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal))
                    yield return (mod.Id, file, ModFileReader.ReadResolved(mod.FileProvider, file));
            }
        }
    }
}
