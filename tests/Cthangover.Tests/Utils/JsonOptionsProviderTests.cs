using System.Text.Json;
using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    public class JsonOptionsProviderTests
    {
        private class TestModel
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string NullableField { get; set; }
        }

        [Fact]
        public void ReadOptions_DeserializesJsonWithLineComment()
        {
            var json = @"{
                // This is a line comment
                ""Id"": ""test"",
                ""Name"": ""Hello"",
                ""Value"": 42
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(model);
            Assert.Equal("test", model.Id);
            Assert.Equal("Hello", model.Name);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void ReadOptions_DeserializesJsonWithBlockComment()
        {
            var json = @"{
                /* Block comment
                   spanning multiple lines */
                ""Id"": ""test"",
                ""Name"": ""Hello"",
                ""Value"": 42
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(model);
            Assert.Equal("test", model.Id);
            Assert.Equal("Hello", model.Name);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void ReadOptions_DeserializesJsonWithBothCommentTypes()
        {
            var json = @"{
                // Line comment before block
                /* Block comment */
                ""Id"": ""test"",
                ""Name"": ""Hello"", // Inline comment
                ""Value"": 42
                // Trailing comment
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(model);
            Assert.Equal("test", model.Id);
            Assert.Equal("Hello", model.Name);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void ReadOptions_DeserializesWithCaseInsensitiveMatching()
        {
            var json = @"{
                ""id"": ""test"",
                ""name"": ""Hello"",
                ""value"": 42
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(model);
            Assert.Equal("test", model.Id);
            Assert.Equal("Hello", model.Name);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void ReadOptions_DeserializesComplexJsonWithComments()
        {
            var json = @"{
                // Mod manifest example
                ""Id"": ""my_mod"",
                ""Name"": ""My Mod"",
                /* Version
                   string */ ""Value"": 1
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(model);
            Assert.Equal("my_mod", model.Id);
            Assert.Equal("My Mod", model.Name);
            Assert.Equal(1, model.Value);
        }

        [Fact]
        public void ReadOptions_ThrowsOnMalformedJson()
        {
            var json = @"{ ""Id"": ""test"", }";

            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions));
        }

        [Fact]
        public void ReadOptions_IsCached_SameInstanceReturned()
        {
            var opts1 = JsonOptionsProvider.ReadOptions;
            var opts2 = JsonOptionsProvider.ReadOptions;

            Assert.Same(opts1, opts2);
        }

        [Fact]
        public void WriteIndentedOptions_ProducesMultiLineOutput()
        {
            var model = new TestModel { Id = "t", Name = "N", Value = 1 };

            var json = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteIndentedOptions);

            Assert.Contains("\n", json);
            Assert.Contains("  ", json);
        }

        [Fact]
        public void WriteIndentedOptions_IsCached_SameInstanceReturned()
        {
            var opts1 = JsonOptionsProvider.WriteIndentedOptions;
            var opts2 = JsonOptionsProvider.WriteIndentedOptions;

            Assert.Same(opts1, opts2);
        }

        [Fact]
        public void WriteCompactOptions_ProducesSingleLineOutput()
        {
            var model = new TestModel { Id = "t", Name = "N", Value = 1 };

            var json = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteCompactOptions);

            Assert.DoesNotContain("\n", json);
        }

        [Fact]
        public void WriteCompactOptions_IsCached_SameInstanceReturned()
        {
            var opts1 = JsonOptionsProvider.WriteCompactOptions;
            var opts2 = JsonOptionsProvider.WriteCompactOptions;

            Assert.Same(opts1, opts2);
        }

        [Fact]
        public void WriteIndentedIgnoreNullOptions_OmitsNullFields()
        {
            var model = new TestModel { Id = "t", Name = "N", Value = 1, NullableField = null };

            var json = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);

            Assert.DoesNotContain("NullableField", json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Value\"", json);
        }

        [Fact]
        public void WriteIndentedIgnoreNullOptions_IncludesNonNullNullField()
        {
            var model = new TestModel { Id = "t", Name = "N", Value = 1, NullableField = "present" };

            var json = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);

            Assert.Contains("NullableField", json);
        }

        [Fact]
        public void WriteIndentedIgnoreNullOptions_IsCached_SameInstanceReturned()
        {
            var opts1 = JsonOptionsProvider.WriteIndentedIgnoreNullOptions;
            var opts2 = JsonOptionsProvider.WriteIndentedIgnoreNullOptions;

            Assert.Same(opts1, opts2);
        }

        [Fact]
        public void DocumentOptions_AllowsCommentsInJsonDocument()
        {
            var json = @"{
                // comment
                ""Id"": ""test"",
                ""Value"": 42
            }";

            using var doc = JsonDocument.Parse(json, JsonOptionsProvider.DocumentOptions);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("Id", out var idProp));
            Assert.Equal("test", idProp.GetString());

            Assert.True(root.TryGetProperty("Value", out var valProp));
            Assert.Equal(42, valProp.GetInt32());
        }

        [Fact]
        public void DocumentOptions_AllowsBlockCommentsInJsonDocument()
        {
            var json = @"{
                /* header */
                ""Id"": ""test"",
                /* footer */
                ""Value"": 42
            }";

            using var doc = JsonDocument.Parse(json, JsonOptionsProvider.DocumentOptions);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("Id", out var idProp));
            Assert.Equal("test", idProp.GetString());
            Assert.True(root.TryGetProperty("Value", out var valProp));
            Assert.Equal(42, valProp.GetInt32());
        }

        [Fact]
        public void DocumentOptions_ThrowsOnMalformedJson()
        {
            var json = @"{ ""Id"": ""test"", }";

            var ex = Assert.ThrowsAny<JsonException>(() =>
                JsonDocument.Parse(json, JsonOptionsProvider.DocumentOptions));
        }

        [Fact]
        public void DocumentOptions_StructIsReturnedByValue()
        {
            var opts1 = JsonOptionsProvider.DocumentOptions;
            var opts2 = JsonOptionsProvider.DocumentOptions;

            Assert.Equal(opts1.CommentHandling, opts2.CommentHandling);
        }

        [Fact]
        public void ReadOptions_RoundTrip_CommentsParsedOutputClean()
        {
            var json = @"{
                // Comment before Id
                ""Id"": ""roundtrip"",
                ""Name"": ""RT"",
                ""Value"": 99 // Inline
            }";

            var model = JsonSerializer.Deserialize<TestModel>(json, JsonOptionsProvider.ReadOptions);
            var serialized = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteCompactOptions);
            var restored = JsonSerializer.Deserialize<TestModel>(serialized, JsonOptionsProvider.ReadOptions);

            Assert.Equal(model.Id, restored.Id);
            Assert.Equal(model.Name, restored.Name);
            Assert.Equal(model.Value, restored.Value);
        }

        [Fact]
        public void WriteIndentedIgnoreNullOptions_ProducesIndentedOutput()
        {
            var model = new TestModel { Id = "t", Name = "N", Value = 1, NullableField = null };

            var json = JsonSerializer.Serialize(model, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);

            Assert.Contains("\n", json);
            Assert.Contains("  ", json);
        }
    }
}
