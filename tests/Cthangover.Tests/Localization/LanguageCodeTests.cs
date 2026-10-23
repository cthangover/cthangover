using Cthangover.Core.Localization;

namespace Engine.Tests.Localization
{
    /// <summary>
    /// Коды языка в любом виде, в котором они встречаются в настройках
    /// (<c>ru-ru</c>, <c>en-en</c>, <c>en_US</c>, регистр), читаются как один язык.
    /// </summary>
    public class LanguageCodeTests
    {
        [Theory]
        [InlineData("en", "en")]
        [InlineData("en-en", "en")]
        [InlineData("en-US", "en")]
        [InlineData("en_us", "en")]
        [InlineData(" EN ", "en")]
        [InlineData("ru", "ru")]
        [InlineData("ru-ru", "ru")]
        [InlineData("RU_RU", "ru")]
        [InlineData("zh", "zh")]
        [InlineData("zh-CN", "zh")]
        [InlineData("es-ES", "es")]
        [InlineData("ja_JP", "ja")]
        [InlineData("ko", "ko")]
        [InlineData("vi-VN", "vi")]
        public void Normalize_SupportedLanguageInAnyForm_ReturnsItsCode(string code, string expected)
        {
            Assert.Equal(expected, LanguageCode.Normalize(code));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("de")]
        [InlineData("de-DE")]
        [InlineData("english")]
        public void Normalize_UnsupportedOrEmpty_ReturnsNull(string code)
        {
            Assert.Null(LanguageCode.Normalize(code));
        }

        [Theory]
        [InlineData("de-AT", "de")]
        [InlineData("Fr_ca", "fr")]
        [InlineData("pt", "pt")]
        public void Primary_AnyCode_ReturnsLowercasePrimarySubtag(string code, string expected)
        {
            Assert.Equal(expected, LanguageCode.Primary(code));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Primary_Empty_ReturnsNull(string code)
        {
            Assert.Null(LanguageCode.Primary(code));
        }

        [Theory]
        [InlineData("en", "en-en", true)]
        [InlineData("EN_us", "en", true)]
        [InlineData("de-AT", "de-DE", true)]
        [InlineData("ru", "en", false)]
        [InlineData(null, "en", false)]
        [InlineData("en", null, false)]
        [InlineData(null, null, false)]
        public void Same_ComparesPrimarySubtags(string a, string b, bool expected)
        {
            Assert.Equal(expected, LanguageCode.Same(a, b));
        }

        [Fact]
        public void Supported_EveryCodeNormalizesToItself()
        {
            foreach (var code in LanguageCode.Supported)
                Assert.Equal(code, LanguageCode.Normalize(code));
        }
    }
}
