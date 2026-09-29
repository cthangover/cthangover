using Cthangover.Core.Scenes.Patching;

namespace Engine.Tests.Scenes
{
    public class MaskMatcherTests
    {
        [Fact]
        public void ExactPattern_MatchesOnlySameText()
        {
            Assert.True(MaskMatcher.IsMatch("MenuContainer/LoadBtn", "MenuContainer/LoadBtn"));
            Assert.False(MaskMatcher.IsMatch("MenuContainer/LoadBtn", "MenuContainer/ExitBtn"));
        }

        [Fact]
        public void Star_MatchesAcrossHierarchySeparators()
        {
            Assert.True(MaskMatcher.IsMatch("MenuContainer/*", "MenuContainer/Sub/Deep"));
            Assert.True(MaskMatcher.IsMatch("*Btn", "MenuContainer/LoadBtn"));
            Assert.False(MaskMatcher.IsMatch("*Btn", "MenuContainer/LoadButton"));
        }

        [Fact]
        public void QuestionMark_MatchesExactlyOneCharacter()
        {
            Assert.True(MaskMatcher.IsMatch("Btn?", "Btn1"));
            Assert.False(MaskMatcher.IsMatch("Btn?", "Btn12"));
            Assert.False(MaskMatcher.IsMatch("Btn?", "Btn"));
        }

        [Fact]
        public void PatternWithoutMatch_ReturnsFalse()
        {
            Assert.False(MaskMatcher.IsMatch("theme_override_colors/*", "text"));
            Assert.False(MaskMatcher.IsMatch("Panel", "Panel/Child"));
        }

        [Fact]
        public void Matching_IsCaseSensitive()
        {
            Assert.False(MaskMatcher.IsMatch("panel", "Panel"));
            Assert.True(MaskMatcher.IsMatch("*", "Panel"));
        }

        [Fact]
        public void EmptyOrNull_ReturnsFalse()
        {
            Assert.False(MaskMatcher.IsMatch(null, "Panel"));
            Assert.False(MaskMatcher.IsMatch("", "Panel"));
            Assert.False(MaskMatcher.IsMatch("*", null));
        }

        [Fact]
        public void HasMask_DetectsWildcards()
        {
            Assert.True(MaskMatcher.HasMask("a*b"));
            Assert.True(MaskMatcher.HasMask("a?b"));
            Assert.False(MaskMatcher.HasMask("plain"));
            Assert.False(MaskMatcher.HasMask(null));
        }
    }
}
