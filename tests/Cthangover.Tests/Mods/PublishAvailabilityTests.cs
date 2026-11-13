using System.Net.Http;
using Cthangover.Core.Mods;
using Mods.ModManager;

namespace Engine.Tests.Mods;

public sealed class PublishAvailabilityTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "publish-availability-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Check_GuiStoreRoundtripRetainsAuthorDestination()
    {
        var config = new ModConfig { CatalogUrls = new(), CatalogTokenFiles = new() };
        var store = new CatalogServerStore(Path.Combine(directory, "catalog_servers.json"));
        var saved = store.Upsert(config, new CatalogServer { Url = "http://127.0.0.1:18080", Name = "Local via GUI" }, new string('A', 1024));
        store.ApplyTo(config);
        var restored = Assert.Single(store.Load(config));
        Assert.Equal("http://127.0.0.1:18080/v1/catalog", restored.Url);
        Assert.Equal(saved.Id, restored.Id);
        Assert.Equal(saved.Name, restored.Name);
        Assert.Equal(saved.Enabled, restored.Enabled);
        Assert.Equal(saved.TokenFile, restored.TokenFile);
        Assert.True(Path.IsPathFullyQualified(restored.TokenFile));
        Assert.Equal(new string('A', 1024), File.ReadAllText(restored.TokenFile).Trim());
        Assert.Equal(restored.TokenFile, config.CatalogTokenFiles[restored.Url]);
        var result = await PublishAvailability.CheckAsync(restored, Author, default);
        Assert.Null(result.ErrorKey);
        Assert.Equal("author", result.Permissions.AuthorId);
        Assert.Equal(new[] { "local_test" }, result.Permissions.AvailableSections);
        Assert.Equal(1, PublishDestination.Select(new[] { new CatalogServer { Url = "https://public.test/catalog.json" }, restored }, null));
    }

    [Theory]
    [InlineData(false, "key.token", "mod_manager/publish_server_disabled")]
    [InlineData(true, null, "mod_manager/publish_no_key")]
    public async Task Check_ExplainsConfigurationWithoutContactingServer(bool enabled, string tokenFile, string expected)
    {
        var result = await PublishAvailability.CheckAsync(new CatalogServer { Enabled = enabled, TokenFile = tokenFile }, Unexpected, default);
        Assert.Equal(expected, result.ErrorKey);
        Assert.Null(result.Permissions);
    }

    [Fact]
    public async Task Check_ReaderKeyRetainsSpecificDenial()
    {
        var result = await PublishAvailability.CheckAsync(Keyed(), Reader, default);
        Assert.Equal("mod_manager/publish_denied", result.ErrorKey);
        Assert.Null(result.Permissions);
    }

    [Fact]
    public async Task Check_NetworkErrorIsVisibleAndCannotPublish()
    {
        var result = await PublishAvailability.CheckAsync(Keyed(), Network, default);
        Assert.Equal("mod_manager/publish_unavailable", result.ErrorKey);
        Assert.Contains("Connection refused", result.ErrorDetail);
        Assert.Null(result.Permissions);
    }

    [Fact]
    public async Task Check_EmptyAuthorScopesExplainMissingSections()
    {
        var result = await PublishAvailability.CheckAsync(Keyed(), NoSections, default);
        Assert.Equal("mod_manager/publish_no_sections", result.ErrorKey);
        Assert.Null(result.Permissions);
    }

    [Fact]
    public async Task Check_AdminWithNoExistingSectionsCanEnterASection()
    {
        var result = await PublishAvailability.CheckAsync(Keyed(), Admin, default);
        Assert.Null(result.ErrorKey);
        Assert.True(result.Permissions.AllowsCustomSection);
        Assert.True(result.Permissions.AllowsSection("first_section"));
    }

    [Fact]
    public async Task Check_LifetimeCancellationEscapes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PublishAvailability.CheckAsync(Keyed(), Cancelled, cancellation.Token));
    }

    [Fact]
    public void ErrorMessages_DoNotExposeArbitraryExceptionPayloads()
    {
        var error = new Exception("secret-response");
        Assert.Equal("mod_manager/publish_unavailable", PublishAvailability.ErrorMessageKey(error));
        Assert.Equal("", PublishAvailability.ErrorMessageDetail(error));
        var retry = new InvalidOperationException("mod_manager/publish_connection", new HttpRequestException("HTTP 503"));
        Assert.Equal("mod_manager/publish_connection", PublishAvailability.ErrorMessageKey(retry));
        Assert.Equal("HTTP 503", PublishAvailability.ErrorMessageDetail(retry));
    }

    private static CatalogServer Keyed() => new() { Url = "http://127.0.0.1:18080/v1/catalog", TokenFile = "author.token" };
    private static Task<PublishingPermissions> Author(CatalogServer server, CancellationToken cancellation)
        => Task.FromResult(new PublishingPermissions { AuthorId = "author", Sections = new() { "local_test" } });
    private static Task<PublishingPermissions> Admin(CatalogServer server, CancellationToken cancellation)
        => Task.FromResult(new PublishingPermissions { AuthorId = "admin", IsAdmin = true, Sections = new() });
    private static Task<PublishingPermissions> NoSections(CatalogServer server, CancellationToken cancellation)
        => Task.FromResult(new PublishingPermissions { AuthorId = "author", Sections = new() });
    private static Task<PublishingPermissions> Reader(CatalogServer server, CancellationToken cancellation)
        => throw new InvalidOperationException("mod_manager/publish_denied");
    private static Task<PublishingPermissions> Network(CatalogServer server, CancellationToken cancellation)
        => throw new HttpRequestException("Connection refused (127.0.0.1:18080)");
    private static Task<PublishingPermissions> Cancelled(CatalogServer server, CancellationToken cancellation)
        => Task.FromCanceled<PublishingPermissions>(cancellation);
    private static Task<PublishingPermissions> Unexpected(CatalogServer server, CancellationToken cancellation)
        => throw new Exception("Configuration failure should not request permissions");

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
