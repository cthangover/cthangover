using System;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Xunit;

namespace Engine.Tests.Mods;

public class ModCategoriesTests
{
    [Fact]
    public void NormalizationPreservesAuthoredSpellingAndFirstOccurrence()
    {
        var categories = ModCategories.Normalize(new[] { " RPG ", null, "rpg", "Weap ons! 世界", "", "   ", "Rpg", "_pack / : @", "Сюжет" });
        Assert.Equal(new[] { "RPG", "Weap ons! 世界", "_pack / : @", "Сюжет" }, categories);
        Assert.Equal("RPG", ModCategories.Primary(categories));
        Assert.Empty(ModCategories.Normalize(null));
        Assert.Equal("other", ModCategories.Primary(Array.Empty<string>()));
        Assert.Equal(new[] { "other" }, ModCategories.Effective(null));
    }

    [Fact]
    public void ClientDoesNotLimitCategoryLengthOrCount()
    {
        var names = Enumerable.Range(0,100).Select(i => "Category " + i).ToList();
        names.Insert(0,new string('界',200));
        names.Add("Local\nname");
        Assert.Equal(names, ModCategories.Normalize(names));
    }

    [Fact]
    public void BaseNamesAreOnlySuggestions()
    {
        Assert.Equal(new[] { "story", "characters", "gameplay", "visual", "audio", "ui", "locale", "library", "engine", "overhaul", "tools", "editor", "debug", "other" }, ModCategories.Base);
        Assert.Equal(new[] { "gameplayy", "Weap ons!", "RPG" }, ModCategories.Normalize(new[] { "gameplayy", "Weap ons!", "RPG" }));
        Assert.True(ModCategories.Comparer.Equals("RPG","rpg"));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("\"RPG\"")]
    [InlineData("[\"RPG\",false]")]
    [InlineData("[null]")]
    public void NonStringArrayIsAJSONTypeError(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var error = Assert.Throws<JsonException>(() => ModCategories.Read(doc.RootElement));
        Assert.Contains("JSON array of strings",error.Message);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ModManifest>("{\"categories\":"+json+"}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CatalogExportEntry>("{\"categories\":"+json+"}"));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("\"RPG\"")]
    [InlineData("[\"RPG\",false]")]
    [InlineData("[null]")]
    public void CatalogSkipsOnlyTheRecordWithAJSONTypeError(string field)
    {
        var json = "{\"mods\":[{\"id\":\"one\",\"categories\":[\"story\"]},{\"id\":\"two\",\"categories\":" + field + "},{\"id\":\"three\",\"categories\":[\"RPG\",\"rpg\",\"Weap ons! 世界\"]}]}";
        var catalog = JsonSerializer.Deserialize<CatalogRoot>(json);
        Assert.Equal(new[] { "one", "three" },catalog.Mods.Select(m=>m.Id));
        Assert.Equal(new[] { "RPG", "Weap ons! 世界" },catalog.Mods[1].Categories);
        var serialized = JsonSerializer.Serialize(catalog);
        var restored = JsonSerializer.Deserialize<CatalogRoot>(serialized);
        Assert.Equal(catalog.Mods.Select(m=>m.Id),restored.Mods.Select(m=>m.Id));
        Assert.Equal(catalog.Mods[1].Categories,restored.Mods[1].Categories);
        Assert.DoesNotContain("\"category\":",serialized);
        var cache = JsonSerializer.Deserialize<CatalogCacheRoot>(json);
        Assert.Equal(new[] { "one", "three" },cache.Mods.Select(m=>m.Id));
    }

    [Fact]
    public void EveryAuthoredStringSurvivesManifestAndCatalogRoundTrip()
    {
        var categories = new[] { "RPG", "Weap ons! 世界", "_pack / : @", "a", "b", "c", new string('я',200) };
        var manifest = new ModManifest { Categories = categories.ToList() };
        var restored = JsonSerializer.Deserialize<ModManifest>(JsonSerializer.Serialize(manifest));
        Assert.Equal(categories,restored.Categories);
        var catalog = new CatalogRoot { Mods = new() { new() { Id="any", Categories=categories.ToList() } } };
        var copy = JsonSerializer.Deserialize<CatalogRoot>(JsonSerializer.Serialize(catalog));
        Assert.Equal(categories,copy.Mods[0].Categories);
    }

    [Fact]
    public void LegacyFieldIsIgnoredWithoutAlias()
    {
        var manifest = JsonSerializer.Deserialize<ModManifest>("{\"category\":\"story\"}");
        Assert.Empty(manifest.Categories);
        Assert.Equal("other",ModCategories.Primary(manifest.Categories));
        Assert.DoesNotContain("\"category\":",JsonSerializer.Serialize(manifest));
    }
}
