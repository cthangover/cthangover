namespace Engine.Tests.Scenarios
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Mods.ScenarioStudio;
    using Engine.Tests.Mods.Mocks;

    /// <summary>
    /// Layer writes are proven through real folders and the public shared-reader output, including refusal before
    /// any writes.
    /// </summary>
    public sealed class StudioSourceSaveTests : IDisposable
    {
        private const string Root = "scenarios/main.scenario";
        private readonly string temporary = Path.Combine(Path.GetTempPath(), "cthangover-studio-source-" + Guid.NewGuid().ToString("N"));
        private readonly FolderModFileProvider source;
        private readonly string layer;

        /// <summary>Each case owns an isolated source folder and destination, never repository or player data.</summary>
        public StudioSourceSaveTests()
        {
            source = new FolderModFileProvider(Path.Combine(temporary, "source"), "studio_save_test");
            layer = Path.Combine(temporary, "layer");
            Directory.CreateDirectory(source.RootPath);
            Directory.CreateDirectory(layer);
        }

        /// <summary>
        /// Changing an include target rebinds through the source reader and copies exact raw inputs, including an
        /// empty fragment.
        /// </summary>
        [Fact]
        public void RawLayerSave_RebindsEditedTargetAndPreservesCrLfAndEmptyInputs()
        {
            const string original = "include_syntax: explicit-v1\r\n---\r\n$include{old.txt}\r\nend\r\n";
            const string edited = "include_syntax: explicit-v1\r\n---\r\n$include{new.txt}\r\nend\r\n";
            const string fragment = "\r\nptext ${who}\r\n$include{empty.txt}\r\n";
            WriteSource(Root, original);
            WriteSource("scenarios/old.txt", "ptext old");
            WriteSource("scenarios/new.txt", fragment);
            WriteSource("scenarios/empty.txt", " \r\n\t");

            Assert.True(StudioSourceSave.TrySave(source, source.Mod, Root, original, edited, layer, out var error));

            Assert.Null(error);
            Assert.Equal(original, source.ReadFileText(Root));
            using var destination = new FolderModFileProvider(layer, "layer");
            var result = ScenarioDefs.ReadSource("layer", Root, destination);
            Assert.NotNull(result);
            Assert.Equal(edited, result.RawText);
            Assert.Equal("include_syntax: explicit-v1\r\n---\r\nptext ${who}\r\nend\r\n", result.ExpandedText);
            Assert.Equal(fragment, destination.ReadFileText("scenarios/new.txt"));
            Assert.Equal(" \r\n\t", destination.ReadFileText("scenarios/empty.txt"));
            Assert.Null(destination.ReadFileText("scenarios/old.txt"));
        }

        /// <summary>
        /// A layer's extensionless file cannot shadow the source's JSON fallback, even when the copied JSON path
        /// is free.
        /// </summary>
        [Fact]
        public void RawLayerSave_ExtensionlessDestinationShadowRefusesBeforeWrites()
        {
            const string raw = "include_syntax: explicit-v1\n---\n$include{parts/body}";
            WriteSource(Root, raw);
            WriteSource("scenarios/parts/body.json", "set value original");
            Write(layer, "scenarios/parts/body", "set value shadow");

            Assert.False(StudioSourceSave.TrySave(source, source.Mod, Root, raw, raw, layer, out var error));

            Assert.Equal("studio/error_layer_source_conflict", error);
            Assert.False(File.Exists(Path.Combine(layer, Root)));
            Assert.False(File.Exists(Path.Combine(layer, "scenarios/parts/body.json")));
            Assert.Equal("set value shadow", File.ReadAllText(Path.Combine(layer, "scenarios/parts/body")));
            Assert.Equal(raw, source.ReadFileText(Root));
        }

        /// <summary>Pre-existing authored layer content is never replaced to make a root's include closure fit.</summary>
        [Fact]
        public void RawLayerSave_ExistingDependencyConflictLeavesEveryFileUntouched()
        {
            const string raw = "include_syntax: explicit-v1\n---\n$include{part.txt}";
            WriteSource(Root, raw);
            WriteSource("scenarios/part.txt", "ptext original");
            Write(layer, "scenarios/part.txt", "ptext user edit");

            Assert.False(StudioSourceSave.TrySave(source, source.Mod, Root, raw, raw, layer, out var error));

            Assert.Equal("studio/error_layer_source_conflict", error);
            Assert.False(File.Exists(Path.Combine(layer, Root)));
            Assert.Equal("ptext user edit", File.ReadAllText(Path.Combine(layer, "scenarios/part.txt")));
            Assert.Equal("ptext original", source.ReadFileText("scenarios/part.txt"));
        }

        /// <summary>Two different observations of one canonical input cannot be silently collapsed into an arbitrary layer copy.</summary>
        [Fact]
        public void RawLayerSave_DifferentRepeatedInputRefusesCoherentCopy()
        {
            const string raw = "include_syntax: explicit-v1\n---\n$include{part.txt}\n$include{part.txt}";
            WriteSource(Root, raw);
            WriteSource("scenarios/part.txt", "unused physical baseline");
            using var changing = new ChangingTextModFileProvider(source, "scenarios/part.txt");

            Assert.False(StudioSourceSave.TrySave(changing, source.Mod, Root, raw, raw, layer, out var error));

            Assert.Equal("studio/error_incoherent_source", error);
            Assert.Empty(Directory.GetFileSystemEntries(layer));
            Assert.Equal(raw, source.ReadFileText(Root));
        }

        /// <summary>Removes only this case's isolated filesystem roots, including when an assertion fails.</summary>
        public void Dispose()
        {
            source.Dispose();
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }

        private void WriteSource(string path, string text)
        {
            Write(source.RootPath, path, text);
        }

        private static void Write(string root, string path, string text)
        {
            var target = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, text);
        }
    }
}
