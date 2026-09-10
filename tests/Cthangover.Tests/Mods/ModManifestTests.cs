using System.Text.Json;
using Cthangover.Core.Mods;

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
    }
}
