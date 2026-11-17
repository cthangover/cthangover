namespace Engine.Tests.Mods
{
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Engine.Tests.Mods.Mocks;

    /// <summary>Checks provider-relative expansion separately from runtime scenario interpolation and JSON string data.</summary>
    public class ExpandModIncludesTests
    {
        private static ModManager CreateModManagerWith(MockModFileProvider provider)
        {
            return ModManager.Instance;
        }

        /// <summary>Explicit scenario syntax cannot resolve runtime names through either extensionless or JSON siblings.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_RuntimeNamesIgnoreSiblingFiles()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            var source = "include_syntax: explicit-v1\n---\naction operation.run reference=${reference}\nptext \"${value}\"";
            provider.AddFile("scenarios/main.scenario", source);
            provider.AddFile("scenarios/reference", "wrong provider data");
            provider.AddFile("scenarios/value.json", "99");

            var result = ModManager.Instance.ReadResolvedText("explicit_fixture", "scenarios/main.scenario", provider);

            Assert.Equal(source, result);
        }

        /// <summary>The root selection reaches non-JSON nested fragments instead of reinterpreting their runtime tokens.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_NestedFragmentsPreserveRuntimeNames()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("scenarios/main.scenario", "include_syntax: explicit-v1\n---\nA ${parent}\n$include{parts/body.txt}");
            provider.AddFile("scenarios/parts/body.txt", "B ${child}\n$include{../tail.txt}");
            provider.AddFile("scenarios/tail.txt", "C ${leaf}");
            provider.AddFile("scenarios/parts/child.json", "must not substitute");

            var result = ModManager.Instance.ReadResolvedText("explicit_fixture", "scenarios/main.scenario", provider);

            Assert.Equal("include_syntax: explicit-v1\n---\nA ${parent}\nB ${child}\nC ${leaf}", result);
        }

        /// <summary>JSON fallback keeps its existing quote shielding and outside-string includes; returned text is not rescanned.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_JsonFallbackUsesExistingJsonRules()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("scenarios/main.scenario", "include_syntax: explicit-v1\n---\n$include{data}");
            provider.AddFile("scenarios/data.json",
                "{\"Runtime\":\"${reference}\",\"Literal\":\"$include{never}\",\"Nested\":${piece}}");
            provider.AddFile("scenarios/piece.json", "[1,2]");
            provider.AddFile("scenarios/reference.json", "99");
            provider.AddFile("scenarios/never.json", "wrong");

            var result = ModManager.Instance.ReadResolvedText("explicit_fixture", "scenarios/main.scenario", provider);

            Assert.Equal("include_syntax: explicit-v1\n---\n"
                + "{\"Runtime\":\"${reference}\",\"Literal\":\"$include{never}\",\"Nested\":[1,2]}", result);
        }

        /// <summary>A declared unsupported root mode refuses even when it would not otherwise contain an include.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_UnknownModeRefuses()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("main.scenario", "include_syntax: future-v9\n---\nend");

            Assert.Null(ModManager.Instance.ReadResolvedText("explicit_fixture", "main.scenario", provider));
        }

        /// <summary>A missing explicit include refuses; it cannot silently become an unresolved runtime variable.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_MissingIncludeRefuses()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("main.scenario", "include_syntax: explicit-v1\n---\n$include{missing}");

            Assert.Null(ModManager.Instance.ReadResolvedText("explicit_fixture", "main.scenario", provider));
        }

        /// <summary>The canonical include cycle remains an error under the selected explicit syntax.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_CycleRefuses()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("main.scenario", "include_syntax: explicit-v1\n---\n$include{part.txt}");
            provider.AddFile("part.txt", "$include{main.scenario}");

            Assert.Null(ModManager.Instance.ReadResolvedText("explicit_fixture", "main.scenario", provider));
        }

        /// <summary>An empty explicit directive is an authoring error rather than an invisible file lookup.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_EmptyIncludeRefuses()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("main.scenario", "include_syntax: explicit-v1\n---\n$include{}");

            Assert.Null(ModManager.Instance.ReadResolvedText("explicit_fixture", "main.scenario", provider));
        }

        /// <summary>Without the reserved root opt-in, scenario files keep the established legacy include behavior.</summary>
        [Fact]
        public void ReadResolvedText_ExplicitScenario_AbsentModeKeepsLegacyIncludes()
        {
            var provider = new MockModFileProvider("explicit_fixture");
            provider.AddFile("main.scenario", "scene: room\n---\nptext \"${value}\"");
            provider.AddFile("value.json", "legacy");

            var result = ModManager.Instance.ReadResolvedText("explicit_fixture", "main.scenario", provider);

            Assert.Equal("scene: room\n---\nptext \"legacy\"", result);
        }

        /// <summary>A legacy text include replaces its marker with the selected provider content.</summary>
        [Fact]
        public void ReadResolvedText_SingleLevel_ResolvesInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${sub.txt}");
            provider.AddFile("sub.txt", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        /// <summary>Legacy recursion expands each provider-relative resource without losing surrounding text.</summary>
        [Fact]
        public void ReadResolvedText_TwoLevel_NestedInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "A ${b.txt}");
            provider.AddFile("b.txt", "B ${c.txt}");
            provider.AddFile("c.txt", "C");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Equal("A B C", result);
        }

        /// <summary>The historical empty legacy marker remains literal for compatibility.</summary>
        [Fact]
        public void ReadResolvedText_EmptyInclude_PassesThrough()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello ${}", result);
        }

        /// <summary>A missing legacy resource refuses resolution instead of returning partially expanded text.</summary>
        [Fact]
        public void ReadResolvedText_NotFound_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${nonexistent}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Null(result);
        }

        /// <summary>An extensionless legacy include retains its JSON fallback path.</summary>
        [Fact]
        public void ReadResolvedText_NotFound_JsonFallback()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${data}");
            provider.AddFile("data.json", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        /// <summary>A direct legacy cycle is rejected by the shared visited-path guard.</summary>
        [Fact]
        public void ReadResolvedText_CircularDirect_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "${a.txt}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Null(result);
        }

        /// <summary>An indirect legacy cycle is rejected before unbounded recursive reads.</summary>
        [Fact]
        public void ReadResolvedText_CircularIndirect_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "${b.txt}");
            provider.AddFile("b.txt", "${a.txt}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Null(result);
        }

        /// <summary>A resource without directives preserves its exact text.</summary>
        [Fact]
        public void ReadResolvedText_NoIncludes_ReturnsSame()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("plain.txt", "Just plain text");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "plain.txt", provider);

            Assert.Equal("Just plain text", result);
        }

        /// <summary>An absent root resource remains absent rather than becoming empty content.</summary>
        [Fact]
        public void ReadResolvedText_NullContent_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "nonexistent.txt", provider);

            Assert.Null(result);
        }

        /// <summary>Independent legacy replacements on one line preserve their relative order.</summary>
        [Fact]
        public void ReadResolvedText_MultipleIncludes_InSameLine()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "${a.txt} ${b.txt}");
            provider.AddFile("a.txt", "Hello");
            provider.AddFile("b.txt", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        /// <summary>A legacy include resolves relative to its containing file.</summary>
        [Fact]
        public void ReadResolvedText_IncludeWithRelativePath()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("dir/main.txt", "${../shared/data.txt}");
            provider.AddFile("shared/data.txt", "Resolved");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "dir/main.txt", provider);

            Assert.Equal("Resolved", result);
        }

        /// <summary>Quoted JSON runtime tokens remain literal even when similarly named resources exist.</summary>
        [Fact]
        public void ReadResolvedText_JsonScenarioVariables_ArePreserved()
        {
            var provider = new MockModFileProvider("test_mod");
            var commands = new[]
            {
                "cset reward \"${money}+0\"",
                "action item.add id=money/coin count=${reward}",
                "ptext \"${reward} coins\""
            };
            var source = JsonSerializer.Serialize(commands);
            provider.AddFile("events.json", source);
            // Even an existing file must not shadow a runtime variable inside a JSON string.
            provider.AddFile("money.json", "99");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            Assert.Equal(source, result);
            Assert.Equal(commands, JsonSerializer.Deserialize<string[]>(result));
        }

        /// <summary>JSON resource expansion outside strings coexists with runtime tokens inside strings.</summary>
        [Fact]
        public void ReadResolvedText_JsonIncludeAndVariables_ResolveAtDifferentStages()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("events.json", "{\"Events\": [${chest}], \"Text\": \"${reward}\"}");
            provider.AddFile("chest.json", "{\"Commands\": [\"ptext \\\"${money}\\\"\", \"action item.add count=${reward}\"]}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            using var json = JsonDocument.Parse(result);
            Assert.Equal("${reward}", json.RootElement.GetProperty("Text").GetString());
            var commands = json.RootElement.GetProperty("Events")[0].GetProperty("Commands");
            Assert.Equal("ptext \"${money}\"", commands[0].GetString());
            Assert.Equal("action item.add count=${reward}", commands[1].GetString());
        }

        /// <summary>Escaped quotes and backslashes preserve string boundaries before later JSON includes.</summary>
        [Fact]
        public void ReadResolvedText_JsonEscapes_DoNotHideFollowingInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            var literal = "path\\\"quoted ${reward}\\";
            provider.AddFile("events.json", "{\"Text\":" + JsonSerializer.Serialize(literal) + ",\"Data\":${data.json}}");
            provider.AddFile("data.json", "[1,2]");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            using var json = JsonDocument.Parse(result);
            Assert.Equal(literal, json.RootElement.GetProperty("Text").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("Data")[1].GetInt32());
        }
    }
}
