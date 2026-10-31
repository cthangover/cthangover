namespace Engine.Tests.Utils
{
    using System.Globalization;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;

    public class JsonSemanticTests
    {
        [Theory]
        [InlineData("\"\\u041F\\u0440\\u0438\"", "\"При\"")]
        [InlineData("\"a \\u0026\\u0026 b\"", "\"a && b\"")]
        [InlineData("1", "1.0")]
        [InlineData("1e2", "100")]
        [InlineData("true", "true")]
        [InlineData("null", "null")]
        [InlineData("[1,\"x\"]", "[1.00,\"x\"]")]
        [InlineData("{\"A\":1,\"B\":null}", "{\"a\":1}")]
        [InlineData("{\"A\":{\"C\":[1]}}", "{\"a\":{\"c\":[1]}}")]
        public void Equal_SameValue(string a, string b)
        {
            Assert.True(JsonSemantic.Equal(JsonNode.Parse(a), JsonNode.Parse(b)));
            Assert.True(JsonSemantic.Equal(JsonNode.Parse(b), JsonNode.Parse(a)));
        }

        [Theory]
        [InlineData("\"a\"", "\"A\"")]
        [InlineData("1", "2")]
        [InlineData("1", "\"1\"")]
        [InlineData("true", "false")]
        [InlineData("[1,2]", "[2,1]")]
        [InlineData("[1]", "[1,1]")]
        [InlineData("{\"A\":1}", "{\"A\":1,\"B\":0}")]
        [InlineData("{\"A\":1}", "[1]")]
        [InlineData("null", "0")]
        public void Equal_DifferentValue(string a, string b)
        {
            Assert.False(JsonSemantic.Equal(JsonNode.Parse(a), JsonNode.Parse(b)));
            Assert.False(JsonSemantic.Equal(JsonNode.Parse(b), JsonNode.Parse(a)));
        }

        [Fact]
        public void Equal_IgnoreStringCase_OnlyWhenAsked()
        {
            Assert.True(JsonSemantic.Equal(JsonNode.Parse("\"Roof\""), JsonNode.Parse("\"roof\""), ignoreStringCase: true));
            Assert.False(JsonSemantic.Equal(JsonNode.Parse("\"Roof\""), JsonNode.Parse("\"roof\"")));
        }

        [Fact]
        public void Equal_Numbers_IgnoreTheCurrentCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NumberGroupSeparator = ".";
            try
            {
                CultureInfo.CurrentCulture = comma;
                Assert.True(JsonSemantic.Equal(JsonNode.Parse("1.5"), JsonNode.Parse("1.50")));
                Assert.False(JsonSemantic.Equal(JsonNode.Parse("1.5"), JsonNode.Parse("15")));
                Assert.False(JsonSemantic.Equal(JsonNode.Parse("1.001"), JsonNode.Parse("1001")));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact]
        public void Equal_CSharpNullAndJsonNull_AreTheSame()
        {
            Assert.True(JsonSemantic.Equal(null, JsonNode.Parse("null")));
            Assert.True(JsonSemantic.Equal(null, null));
            Assert.False(JsonSemantic.Equal(null, JsonNode.Parse("{}")));
        }
    }
}
