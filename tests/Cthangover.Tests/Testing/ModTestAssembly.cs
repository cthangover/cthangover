namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Mods.Testing;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>Compiles independent discovery fixtures so invalid attributes cannot pollute other contract tests.</summary>
    internal static class ModTestAssembly
    {
        public static Assembly Compile(string declarations)
        {
            var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var paths = trusted.Concat(new[] { typeof(ModTestAttribute).Assembly.Location, typeof(Godot.Node).Assembly.Location }).Distinct(StringComparer.OrdinalIgnoreCase);
            var tree = CSharpSyntaxTree.ParseText("using System.Threading.Tasks; using Cthangover.Core.Mods.Testing; " + declarations);
            var compilation = CSharpCompilation.Create("discovery_contract_" + Guid.NewGuid().ToString("N"), new[] { tree },
                paths.Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            return Assembly.Load(stream.ToArray());
        }
    }
}
