using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    public class VersionHelperTests
    {
        // ─── Semver (strict numeric) ───

        [Fact]
        public void Compare_SemverMajor_AGreater()
        {
            Assert.True(VersionHelper.Compare("2.0", "1.0") > 0);
        }

        [Fact]
        public void Compare_SemverMajor_ALess()
        {
            Assert.True(VersionHelper.Compare("1.0", "2.0") < 0);
        }

        [Fact]
        public void Compare_SemverMinor_Different()
        {
            Assert.True(VersionHelper.Compare("1.2", "1.1") > 0);
        }

        [Fact]
        public void Compare_SemverBuild_Different()
        {
            Assert.True(VersionHelper.Compare("1.0.3", "1.0.2") > 0);
        }

        [Fact]
        public void Compare_SemverRevision_Different()
        {
            Assert.True(VersionHelper.Compare("1.0.0.5", "1.0.0.4") > 0);
        }

        [Fact]
        public void Compare_Semver_Equal()
        {
            Assert.Equal(0, VersionHelper.Compare("1.2.3", "1.2.3"));
        }

        [Fact]
        public void Compare_Semver_LexicographicTrap_9vs10()
        {
            Assert.True(VersionHelper.Compare("10.0", "9.0") > 0);
        }

        [Fact]
        public void Compare_Semver_ZeroVersions()
        {
            Assert.True(VersionHelper.Compare("0.1", "0.0") > 0);
        }

        // ─── Free-form (string fallback) ───

        [Fact]
        public void Compare_Freeform_SimpleGreater()
        {
            Assert.True(VersionHelper.Compare("Enigma2.0", "Enigma1.5") > 0);
        }

        [Fact]
        public void Compare_Freeform_SimpleLess()
        {
            Assert.True(VersionHelper.Compare("Enigma1.0", "Enigma2.0") < 0);
        }

        [Fact]
        public void Compare_Freeform_AlphaTags()
        {
            Assert.True(VersionHelper.Compare("alpha-2", "alpha-1") > 0);
            Assert.True(VersionHelper.Compare("beta-1", "alpha-9") > 0);
        }

        [Fact]
        public void Compare_Freeform_Equal()
        {
            Assert.Equal(0, VersionHelper.Compare("Enigma1.5", "Enigma1.5"));
        }

        [Fact]
        public void Compare_Freeform_PrefixOnly()
        {
            Assert.True(VersionHelper.Compare("v2", "v1") > 0);
        }

        [Fact]
        public void Compare_Freeform_CaseSensitive()
        {
            int cmp = VersionHelper.Compare("Enigma", "enigma");
            Assert.True(cmp < 0);
        }

        // ─── Letter in middle of version ───

        [Fact]
        public void Compare_LetterInMiddle_Equal()
        {
            Assert.Equal(0, VersionHelper.Compare("1b.2.0", "1b.2.0"));
        }

        [Fact]
        public void Compare_LetterInMiddle_MinorBump()
        {
            Assert.True(VersionHelper.Compare("1b.2.1", "1b.2.0") > 0);
        }

        [Fact]
        public void Compare_LetterInMiddle_PatchBump()
        {
            Assert.True(VersionHelper.Compare("1b.2.5", "1b.2.1") > 0);
        }

        [Fact]
        public void Compare_LetterInMiddle_VersionJump()
        {
            Assert.True(VersionHelper.Compare("2b.0.0", "1b.9.9") > 0);
        }

        [Fact]
        public void Compare_LetterInMiddle_DifferentLetter()
        {
            int cmp = VersionHelper.Compare("1c.0.0", "1b.0.0");
            Assert.True(cmp > 0);
        }

        [Fact]
        public void Compare_LetterInMiddle_ShorterWins()
        {
            int cmp = VersionHelper.Compare("1b.2", "1b.2.0");
            Assert.True(cmp == 0);
        }

        // ─── Mixed: semver vs free-form (both unparseable as Version) ───

        [Fact]
        public void Compare_SingleComponent()
        {
            Assert.True(VersionHelper.Compare("2", "1") > 0);
            Assert.True(VersionHelper.Compare("1", "2") < 0);
        }

        [Fact]
        public void Compare_MajorMinor_AsNumbers()
        {
            Assert.True(VersionHelper.Compare("2.0", "1.0") > 0);
        }

        // ─── Edge cases ───

        [Fact]
        public void Compare_BothNull_Equal()
        {
            Assert.Equal(0, VersionHelper.Compare(null, null));
        }

        [Fact]
        public void Compare_ANull_ALess()
        {
            Assert.True(VersionHelper.Compare(null, "1.0") < 0);
        }

        [Fact]
        public void Compare_BNull_AGreater()
        {
            Assert.True(VersionHelper.Compare("1.0", null) > 0);
        }

        [Fact]
        public void Compare_BothEmpty_Equal()
        {
            Assert.Equal(0, VersionHelper.Compare("", ""));
        }

        [Fact]
        public void Compare_Whitespace_Trimmed()
        {
            Assert.Equal(0, VersionHelper.Compare("  1.0  ", "1.0"));
        }

        [Fact]
        public void Compare_EmptyVsSemver()
        {
            Assert.True(VersionHelper.Compare("", "1.0") < 0);
        }

        // ─── Helpers ───

        [Fact]
        public void IsGreaterThan_True()
        {
            Assert.True(VersionHelper.IsGreaterThan("2.0", "1.0"));
        }

        [Fact]
        public void IsGreaterThan_False()
        {
            Assert.False(VersionHelper.IsGreaterThan("1.0", "2.0"));
        }

        [Fact]
        public void IsLessThan_True()
        {
            Assert.True(VersionHelper.IsLessThan("1.0", "2.0"));
        }

        [Fact]
        public void IsLessThan_False()
        {
            Assert.False(VersionHelper.IsLessThan("2.0", "1.0"));
        }

        [Fact]
        public void IsGreaterThan_Equal()
        {
            Assert.False(VersionHelper.IsGreaterThan("1.0", "1.0"));
        }

        [Fact]
        public void IsLessThan_Equal()
        {
            Assert.False(VersionHelper.IsLessThan("1.0", "1.0"));
        }
    }
}
