using Cthangover.Core.Localization;

namespace Engine.Tests.Localization
{
    public class BootMessagesTests
    {
        [Fact]
        public void FirstLaunchHint_Russian_ReturnsRussianText()
        {
            Assert.Equal(
                "Первый запуск может быть долгим, выполняется компиляция и кеширование...",
                BootMessages.FirstLaunchHint("ru-ru"));
        }

        [Fact]
        public void FirstLaunchHint_English_ReturnsEnglishText()
        {
            var hint = BootMessages.FirstLaunchHint("en");

            Assert.Contains("first launch", hint);
            Assert.DoesNotContain("Первый запуск", hint);
        }

        [Fact]
        public void FirstLaunchHint_Chinese_ReturnsChineseText()
        {
            var hint = BootMessages.FirstLaunchHint("zh");

            Assert.Contains("首次启动", hint);
            Assert.DoesNotContain("Первый запуск", hint);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("de")]
        [InlineData("ru")]
        [InlineData("en-us")]
        public void FirstLaunchHint_UnknownLanguage_FallsBackToEnglish(string language)
        {
            Assert.Equal(
                BootMessages.FirstLaunchHint("en"),
                BootMessages.FirstLaunchHint(language));
        }
    }
}
