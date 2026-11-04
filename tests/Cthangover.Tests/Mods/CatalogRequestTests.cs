using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class CatalogRequestTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "catalog-request-" + Guid.NewGuid().ToString("N"));
        private string tokenFile;
        private string target;

        public CatalogRequestTests()
        {
            Directory.CreateDirectory(root);
            tokenFile = Path.Combine(root, "local.token");
            File.WriteAllText(tokenFile, new string('A', 1024) + "\n");
        }

        [Theory]
        [InlineData("https://mods.example/v1/catalog")]
        [InlineData("http://127.0.0.1:18080/v1/catalog")]
        public void FileBackedBearerIsScopedToTheRequest(string url)
        {
            using var request = CatalogRequest.Create(url, tokenFile);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            Assert.Equal(new string('A', 1024), request.Headers.Authorization.Parameter);
            File.WriteAllText(tokenFile, new string('B', 1024));
            using var updated = CatalogRequest.Create(url, tokenFile);
            Assert.Equal(new string('B', 1024), updated.Headers.Authorization.Parameter);
            using var publicRequest = CatalogRequest.Create("https://public.example/catalog.json");
            Assert.Null(publicRequest.Headers.Authorization);
        }

        [Theory]
        [InlineData("http://mods.example/v1/catalog")]
        [InlineData("http://localhost/v1/catalog")]
        [InlineData("https://user:password@mods.example/v1/catalog")]
        [InlineData("https://mods.example/v1/catalog?token=x")]
        [InlineData("https://mods.example/v1/catalog#fragment")]
        public void UnsafePrivateEndpointsAreRejected(string url)
        {
            target = url;
            Assert.Throws<InvalidOperationException>(CreatePrivateRequest);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("\r\nAuthorization: stolen")]
        public void InvalidTokenNeverAppearsInError(string value)
        {
            File.WriteAllText(tokenFile, value);
            target = "https://mods.example/v1/catalog";
            var error = Assert.Throws<InvalidOperationException>(CreatePrivateRequest);
            Assert.DoesNotContain(value, error.Message);
        }

        [Fact]
        public void ConfigRoundTripPreservesExactUrlsAndFilePaths()
        {
            var original = new ModConfig
            {
                CatalogUrls = new List<string> { "https://private.example/v1/catalog", "https://public.example/catalog.json" },
                CatalogTokenFiles = new Dictionary<string, string> { ["https://private.example/v1/catalog"] = tokenFile },
            };
            var restored = JsonSerializer.Deserialize<ModConfig>(JsonSerializer.Serialize(original));
            Assert.Equal(original.CatalogUrls, restored.CatalogUrls);
            Assert.Equal(original.CatalogTokenFiles, restored.CatalogTokenFiles);
        }

        private void CreatePrivateRequest() => CatalogRequest.Create(target, tokenFile).Dispose();
        public void Dispose() => Directory.Delete(root, true);
    }
}
