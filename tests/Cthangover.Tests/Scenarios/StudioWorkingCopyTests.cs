namespace Engine.Tests.Scenarios
{
    using Cthangover.Mods.ScenarioStudio;

    /// <summary>
    /// Authored raw text retains its baseline and exact row ownership while edits shift the dock's displayed
    /// coordinates.
    /// </summary>
    public sealed class StudioWorkingCopyTests
    {
        /// <summary>Unrelated edits retain CRLF, include directives and runtime tokens instead of persisting expanded content.</summary>
        [Fact]
        public void RawCopy_UnrelatedEditPreservesCrLfHeaderIncludesAndRuntimeTokens()
        {
            const string raw = "include_syntax: explicit-v1\r\n---\r\n$include{body.txt}\r\nptext ${value}\r\nend\r\n";
            var copy = new StudioWorkingCopy("fixture", raw);

            copy.Replace(4, "end # edited");

            Assert.Equal(raw, copy.OriginalText);
            Assert.Equal(raw.Replace("end\r\n", "end # edited\r\n"), copy.Text);
            Assert.True(copy.CanNavigateOriginalLine(3));
            Assert.False(copy.CanNavigateOriginalLine(5));
            var mixed = new StudioWorkingCopy("mixed", "header\r\n$include{body.txt}\nend");
            mixed.Replace(2, "end # edited");
            Assert.Equal("header\r\n$include{body.txt}\nend # edited", mixed.Text);
        }

        /// <summary>Inserted display rows have no runtime origin; retained rows still address their original authored coordinate.</summary>
        [Fact]
        public void RawCopy_ShiftedRetainedRowsKeepOriginalCoordinates()
        {
            var copy = new StudioWorkingCopy("fixture", "set first 1\n$include{body.txt}\nend");

            copy.Insert(0, "# new row");

            Assert.Equal(0, copy.OriginalNumber(1));
            Assert.Equal(2, copy.OriginalNumber(3));
            Assert.True(copy.CanNavigateOriginalLine(2));
            Assert.False(copy.CanNavigateOriginalLine(0));
            Assert.False(copy.CanNavigateOriginalLine(99));
        }

        /// <summary>A whole-text edit never approximates shifted or changed rows by their coincidentally equal display number.</summary>
        [Fact]
        public void RawCopy_ChangedWholeTextRowsRefuseNavigationUntilRebind()
        {
            var copy = new StudioWorkingCopy("fixture", "set first 1\n$include{body.txt}\nend");

            copy.ReplaceAll("set first 1\nptext added\n$include{body.txt}\nend");

            Assert.True(copy.CanNavigateOriginalLine(1));
            Assert.False(copy.CanNavigateOriginalLine(2));
            Assert.False(copy.CanNavigateOriginalLine(3));
        }
    }
}
