using System.Text.Json;
using Mods.ModManager;

namespace Engine.Tests.Mods;

public class PublishingPermissionsTests
{
    [Fact]
    public void AvailableSections_OffersLiteralGrantsBeforeTheFirstUpload()
    {
        var original = JsonSerializer.Deserialize<PublishingPermissions>("""
            {"author_id":"author","author":"Author","sections":[],"section_patterns":["first_section","world_*"],"is_admin":false,"max_archive_bytes":1024,
             "author_consent":{"version":"v1","url":"https://example.test/consent","accepted":false}}
            """);
        var restored = JsonSerializer.Deserialize<PublishingPermissions>(JsonSerializer.Serialize(original));
        Assert.Equal(original.AuthorId, restored.AuthorId);
        Assert.Equal(original.Author, restored.Author);
        Assert.Equal(original.Sections, restored.Sections);
        Assert.Equal(original.SectionPatterns, restored.SectionPatterns);
        Assert.Equal(original.IsAdmin, restored.IsAdmin);
        Assert.Equal(original.MaxArchiveBytes, restored.MaxArchiveBytes);
        Assert.Equal(original.AuthorConsent.Version, restored.AuthorConsent.Version);
        Assert.Equal(original.AuthorConsent.Url, restored.AuthorConsent.Url);
        Assert.Equal(original.AuthorConsent.Accepted, restored.AuthorConsent.Accepted);
        Assert.Equal(new[] { "first_section" }, restored.AvailableSections);
        Assert.True(restored.AllowsCustomSection);
        Assert.True(restored.AllowsSection("first_section"));
        Assert.True(restored.AllowsSection("world_new"));
        Assert.False(restored.AllowsSection("secret"));
        Assert.False(restored.AllowsSection("world_*"));
    }

    [Fact]
    public void AvailableSections_DeduplicatesAndRejectsNonIdentifiers()
    {
        var permissions = new PublishingPermissions { Sections = new() { "story", "story", "../bad" }, SectionPatterns = new() { "story", "future", "other_*" } };
        Assert.Equal(new[] { "story", "future" }, permissions.AvailableSections);
        Assert.False(permissions.IsAdmin);
        Assert.False(permissions.AllowsSection("../bad"));
    }
}
