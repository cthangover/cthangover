using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Supplies <c>build_property.project_dir</c> to the Godot source generator
    /// so it computes script paths relative to the project root exactly like the
    /// main <c>dotnet build</c> does.
    /// </summary>
    internal sealed class ModAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string _projectDir;

        public ModAnalyzerConfigOptions(string projectDir) => _projectDir = projectDir ?? string.Empty;

        public override bool TryGetValue(string key, out string value)
        {
            // Case-insensitive on purpose: MSBuild preserves the property name's
            // casing in "build_property.<Name>" keys, and the Godot generators
            // ask for 'GodotProjectDir'/'GodotProjectDirBase64' exactly as the
            // Sdk spells them. Every key below maps to a real Sdk property.
            switch (key?.ToLowerInvariant())
            {
                case "build_property.project_dir":
                case "build_property.project_directory":
                case "build_property.godotprojectdir":
                    value = _projectDir;
                    return true;

                case "build_property.godotprojectdirbase64":
                    value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_projectDir));
                    return true;

                case "build_property.isgodottoolsproject":
                    // The Sdk leaves this property empty for the game project
                    // itself; it is 'true' only for the editor tool assembly.
                    // ScriptPathAttributeGenerator and GodotPluginsInitializer
                    // bail out when it is 'true', which would strip
                    // [ScriptPathAttribute] from runtime-compiled mods and kill
                    // their [Signal]s, so mirror the game project here.
                    value = string.Empty;
                    return true;

                case "build_property.godotsourcegenerators":
                case "build_property.godotdisabledsourcegenerators":
                    // No generators disabled.
                    value = string.Empty;
                    return true;
            }

            value = null;
            return false;
        }
    }
}
