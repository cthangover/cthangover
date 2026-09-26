using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    public class ModManifestTests
    {
        [Fact]
        public void Deserialize_AllFields()
        {
            var json = @"{
                ""id"": ""test_mod"",
                ""name"": ""Test Mod"",
                ""author"": ""Tester"",
                ""description"": ""A test mod"",
                ""version"": ""1.0.0"",
                ""sources"": [""scripts/*.cs""],
                ""depends"": [""core>=1.0.0""]
            }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.NotNull(manifest);
            Assert.Equal("test_mod", manifest.Id);
            Assert.Equal("Test Mod", manifest.Name);
            Assert.Equal("Tester", manifest.Author);
            Assert.Equal("A test mod", manifest.Description);
            Assert.Equal("1.0.0", manifest.Version);
            Assert.Single(manifest.Sources);
            Assert.Equal("scripts/*.cs", manifest.Sources[0]);
            Assert.Single(manifest.Depends);
            Assert.Equal("core", manifest.Depends[0].ModId);
            Assert.True(manifest.Depends[0].IsValid);
            Assert.Single(manifest.DependencyIds);
            Assert.Equal("core", manifest.DependencyIds[0]);
        }

        [Fact]
        public void Deserialize_MinimalFields()
        {
            var json = @"{ ""id"": ""minimal"" }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.NotNull(manifest);
            Assert.Equal("minimal", manifest.Id);
            Assert.Null(manifest.Name);
            Assert.Null(manifest.Sources);
            Assert.Null(manifest.Depends);
            Assert.Null(manifest.Version);
        }

        [Fact]
        public void ModInfo_DisplayTitle_uses_Name_when_present()
        {
            var info = new ModInfo { Id = "test", Name = "Test Mod" };
            Assert.Equal("Test Mod", info.DisplayTitle);
        }

        [Fact]
        public void ModInfo_DisplayTitle_falls_back_to_Id_when_Name_is_null()
        {
            var info = new ModInfo { Id = "test_no_name" };
            Assert.Equal("test_no_name", info.DisplayTitle);
        }

        [Fact]
        public void ModInfo_DisplayTitle_falls_back_to_Id_when_Name_is_empty()
        {
            var info = new ModInfo { Id = "id_only", Name = "" };
            Assert.Equal("id_only", info.DisplayTitle);
        }

        [Fact]
        public void ModInfo_Version_delegates_to_Manifest()
        {
            var info = new ModInfo
            {
                Id = "test",
                Manifest = new ModManifest { Version = "2.1.0" }
            };
            Assert.Equal("2.1.0", info.Version);
        }

        [Fact]
        public void ModInfo_Version_is_null_when_Manifest_is_null()
        {
            var info = new ModInfo { Id = "test" };
            Assert.Null(info.Version);
        }

        [Fact]
        public void Deserialize_GdSources()
        {
            var json = @"{
                ""id"": ""gd_mod"",
                ""name"": ""GDScript Mod"",
                ""version"": ""1.0.0"",
                ""gd_sources"": [""scripts/*.gd"", ""battle/**/*.gd""]
            }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.NotNull(manifest);
            Assert.NotNull(manifest.GdSources);
            Assert.Equal(2, manifest.GdSources.Count);
            Assert.Equal("scripts/*.gd", manifest.GdSources[0]);
            Assert.Equal("battle/**/*.gd", manifest.GdSources[1]);
        }

        [Fact]
        public void Deserialize_GdSources_NotPresent_ReturnsNull()
        {
            var json = @"{ ""id"": ""no_gd"" }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.NotNull(manifest);
            Assert.Null(manifest.GdSources);
        }

        [Fact]
        public void Deserialize_Category()
        {
            var json = @"{ ""id"": ""cat_mod"", ""category"": ""gameplay"" }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(manifest);
            Assert.Equal(CategoryType.Gameplay, manifest.Category);
        }

        [Fact]
        public void Deserialize_Category_NotPresent_ReturnsNull()
        {
            var json = @"{ ""id"": ""no_cat"" }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(manifest);
            Assert.Null(manifest.Category);
        }

        [Fact]
        public void Deserialize_Category_MultipleFlags()
        {
            var json = @"{ ""id"": ""multi_cat"", ""category"": ""library, gameplay"" }";

            var manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(manifest);
            Assert.Equal(CategoryType.Library | CategoryType.Gameplay, manifest.Category);
        }

        [Fact]
        public void RoundTrip_AllFields()
        {
            var original = new ModManifest
            {
                Id = "round_trip",
                Name = "Round Trip",
                Author = "Tester",
                Description = "Round-trip manifest",
                Version = "1.2.3",
                Category = CategoryType.Library | CategoryType.Tools | CategoryType.Editor | CategoryType.Debug,
                Sources = new List<string> { "src/*.cs" },
                GdSources = new List<string> { "gd/*.gd" },
                DependsNative = new List<string> { "native/*.dll" },
                ConflictsWith = new List<string> { "rival_mod" },
                Depends = new List<ModDependency> { ModDependency.Parse("core>=1") },
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
            var restored = JsonSerializer.Deserialize<ModManifest>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(restored);
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Author, restored.Author);
            Assert.Equal(original.Description, restored.Description);
            Assert.Equal(original.Version, restored.Version);
            Assert.Equal(CategoryType.Library | CategoryType.Tools | CategoryType.Editor | CategoryType.Debug, restored.Category);
            Assert.Equal(original.Sources, restored.Sources);
            Assert.Equal(original.GdSources, restored.GdSources);
            Assert.Equal(original.DependsNative, restored.DependsNative);
            Assert.Equal(original.ConflictsWith, restored.ConflictsWith);
            Assert.Single(restored.Depends);
            Assert.Equal("core", restored.Depends[0].ModId);
            Assert.Equal(">=", restored.Depends[0].Operator);
            Assert.Equal("1", restored.Depends[0].VersionRaw);
        }
    }
}
