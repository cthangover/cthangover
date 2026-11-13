using Cthangover.Core.Mods;
using Mods.ModManager;

namespace Engine.Tests.Mods;

public class PublishDestinationTests
{
    [Fact]
    public void Select_RequiresChoiceUnlessSingleOrPreviouslySuccessful()
    {
        var first = new CatalogServer { Name = "Local", Url = "http://127.0.0.1:1001/a/v1/catalog" };
        var second = new CatalogServer { Name = "Local", Url = "http://127.0.0.1:1002/b/v1/catalog" };
        Assert.Equal(-1, PublishDestination.Select(new List<CatalogServer>(), null));
        Assert.Equal(0, PublishDestination.Select(new List<CatalogServer> { first }, null));
        Assert.Equal(-1, PublishDestination.Select(new List<CatalogServer> { first, second }, null));
        Assert.Equal(1, PublishDestination.Select(new List<CatalogServer> { first, second }, second.Url));
        Assert.Equal(-1, PublishDestination.Select(new List<CatalogServer> { first, second }, "https://removed.test/v1/catalog"));
        Assert.NotEqual(PublishDestination.Label(first), PublishDestination.Label(second));
        Assert.Contains(first.Url, PublishDestination.Label(first));
        Assert.Contains(second.Url, PublishDestination.Label(second));
    }
    [Fact]
    public void Select_PrefersOnlyEnabledKeyedServerAmongPublicSources()
    {
        var publicServer = new CatalogServer { Url = "https://public.test/catalog.json" };
        var author = new CatalogServer { Url = "http://127.0.0.1:18080/v1/catalog", TokenFile = "author.token" };
        var disabled = new CatalogServer { Url = "https://disabled.test/v1/catalog", TokenFile = "other.token", Enabled = false };
        var servers = new List<CatalogServer> { publicServer, author, disabled };
        Assert.Equal(1, PublishDestination.Select(servers, null));
        Assert.Equal(1, PublishDestination.Select(servers, "https://removed.test/v1/catalog"));
        Assert.Equal(2, PublishDestination.Select(servers, disabled.Url));
        servers.Add(new CatalogServer { Url = "https://another.test/v1/catalog", TokenFile = "another.token" });
        Assert.Equal(-1, PublishDestination.Select(servers, null));
    }
}
