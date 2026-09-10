using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    public class StringParsingTests
    {
        [Fact]
        public void GetIncludesInQuotes_ReturnsVariables()
        {
            var text = "Hello ${name}, your balance is ${balance}";
            var result = text.GetIncludesInQuotes("${", "}");

            Assert.Equal(2, result.Count);
            Assert.Contains("name", result);
            Assert.Contains("balance", result);
        }

        [Fact]
        public void GetIncludesInQuotes_NoMatches_ReturnsEmpty()
        {
            var text = "Hello world";
            var result = text.GetIncludesInQuotes("${", "}");

            Assert.Empty(result);
        }

        [Fact]
        public void GetIncludesInQuotes_EmptyString_ReturnsEmpty()
        {
            var result = "".GetIncludesInQuotes("${", "}");
            Assert.Empty(result);
        }

        [Fact]
        public void GetIncludesInQuotes_ThrowsOnUnclosed()
        {
            var text = "Hello ${name";

            Assert.Throws<ArgumentException>(() =>
                text.GetIncludesInQuotes("${", "}"));
        }

        [Fact]
        public void Enums_Parse_ReturnsCorrectValue()
        {
            var result = Enums<DayOfWeek>.Parse("Monday");
            Assert.Equal(DayOfWeek.Monday, result);
        }

        [Fact]
        public void Enums_Parse_IsCaseInsensitive()
        {
            var result = Enums<DayOfWeek>.Parse("monday");
            Assert.Equal(DayOfWeek.Monday, result);
        }
    }
}
