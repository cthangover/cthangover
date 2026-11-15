using System;
using System.IO;
using System.Text;
using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    /// <summary>
    /// File-backed contracts for exact UTF-8/LF rotation, retention and recoverable failures; each instance
    /// owns and removes its isolated temporary directory.
    /// </summary>
    public sealed class RollingLogWriterTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "cthangover-rolling-" + Guid.NewGuid().ToString("N"));
        private string LogPath
        {
            get
            {
                return Path.Combine(directory, "sample.log");
            }
        }

        /// <summary>
        /// Checks active/newest archive order and the total retained file budget after repeated rotation.
        /// </summary>
        [Fact]
        public void RotationKeepsNewestThreeFilesInOrder()
        {
            using var sink = new RollingLogWriter(LogPath, 2, 3);
            for (var i = 1; i <= 5; i++)
            {
                sink.WriteLine(i.ToString());
            }

            Assert.Equal("5\n", File.ReadAllText(LogPath));
            Assert.Equal("4\n", File.ReadAllText(LogPath + ".1"));
            Assert.Equal("3\n", File.ReadAllText(LogPath + ".2"));
            Assert.Equal(3, Directory.GetFiles(directory).Length);
        }

        /// <summary>
        /// Checks startup rotation preserves existing active/archive order before accepting another record.
        /// </summary>
        [Fact]
        public void ExistingOversizedFileRotatesDuringConstruction()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(LogPath, "old-active\n");
            File.WriteAllText(LogPath + ".1", "previous\n");
            File.WriteAllText(LogPath + ".2", "oldest\n");
            using var sink = new RollingLogWriter(LogPath, 4, 3);
            Assert.Equal(string.Empty, File.ReadAllText(LogPath));
            Assert.Equal("old-active\n", File.ReadAllText(LogPath + ".1"));
            Assert.Equal("previous\n", File.ReadAllText(LogPath + ".2"));
            sink.WriteLine("new");
            Assert.Equal("new\n", File.ReadAllText(LogPath));
            Assert.Equal(3, Directory.GetFiles(directory).Length);
        }

        /// <summary>
        /// Checks append semantics and inclusion of preexisting bytes in the next rotation boundary.
        /// </summary>
        [Fact]
        public void ExistingSmallFileAppendsAndItsBytesCountTowardLimit()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(LogPath, "a\n");
            using var sink = new RollingLogWriter(LogPath, 4, 3);
            sink.WriteLine("b");
            Assert.Equal("a\nb\n", File.ReadAllText(LogPath));
            Assert.False(File.Exists(LogPath + ".1"));
            sink.WriteLine("c");
            Assert.Equal("a\nb\n", File.ReadAllText(LogPath + ".1"));
            Assert.Equal("c\n", File.ReadAllText(LogPath));
        }

        /// <summary>
        /// Checks exact multibyte UTF-8 and LF bytes, including the boundary and absence of a byte-order
        /// marker.
        /// </summary>
        [Fact]
        public void Utf8BytesAndLfDetermineTheExactBoundaryWithoutBom()
        {
            using var sink = new RollingLogWriter(LogPath, 7, 3);
            sink.WriteLine("Ж");
            sink.WriteLine("€");
            Assert.Equal(Encoding.UTF8.GetBytes("Ж\n€\n"), File.ReadAllBytes(LogPath));
            Assert.False(File.Exists(LogPath + ".1"));
            sink.WriteLine("a");
            Assert.Equal(Encoding.UTF8.GetBytes("Ж\n€\n"), File.ReadAllBytes(LogPath + ".1"));
            Assert.Equal(new byte[]
            {
                97,
                10
            }, File.ReadAllBytes(LogPath));
        }

        /// <summary>
        /// Checks oversized records are never split, with rotation deferred until the following record.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TinyLimitKeepsAnOversizedRecordIntact(int limit)
        {
            using var sink = new RollingLogWriter(LogPath, limit, 3);
            sink.WriteLine("long record");
            Assert.Equal("long record\n", File.ReadAllText(LogPath));
            Assert.False(File.Exists(LogPath + ".1"));
            sink.WriteLine("x");
            Assert.Equal("long record\n", File.ReadAllText(LogPath + ".1"));
            Assert.Equal("x\n", File.ReadAllText(LogPath));
        }

        /// <summary>
        /// Checks a one-file budget replaces old content without producing archives.
        /// </summary>
        [Fact]
        public void SingleFileBudgetRetainsOnlyCurrentRecords()
        {
            using var sink = new RollingLogWriter(LogPath, 2, 1);
            sink.WriteLine("a");
            sink.WriteLine("b");
            Assert.Equal("b\n", File.ReadAllText(LogPath));
            Assert.Single(Directory.GetFiles(directory));
        }

        /// <summary>
        /// Checks pruning of excess numeric archives while preserving unrelated files and nonnumeric suffixes.
        /// </summary>
        [Fact]
        public void LoweredRetentionPrunesOnlyNumericArchivesOfThisSink()
        {
            Directory.CreateDirectory(directory);
            foreach (var name in new[]
            {
                "sample.log.1",
                "sample.log.2",
                "sample.log.3",
                "sample.log.10",
                "sample.log.notes",
                "other.log.3"
            }

            )
            {
                File.WriteAllText(Path.Combine(directory, name), name);
            }

            using var sink = new RollingLogWriter(LogPath, 100, 2);
            Assert.True(File.Exists(LogPath + ".1"));
            Assert.False(File.Exists(LogPath + ".2"));
            Assert.False(File.Exists(LogPath + ".3"));
            Assert.False(File.Exists(LogPath + ".10"));
            Assert.True(File.Exists(LogPath + ".notes"));
            Assert.True(File.Exists(Path.Combine(directory, "other.log.3")));
        }

        /// <summary>
        /// Checks continued append after a blocked archive rename and recovery on the next record after the
        /// obstruction is removed.
        /// </summary>
        [Fact]
        public void FailedRenameContinuesCurrentFileAndLaterRotationRecovers()
        {
            using var sink = new RollingLogWriter(LogPath, 2, 3);
            sink.WriteLine("a");
            Directory.CreateDirectory(LogPath + ".1");
            sink.WriteLine("b");
            Assert.Equal("a\nb\n", File.ReadAllText(LogPath));
            Directory.Delete(LogPath + ".1");
            sink.WriteLine("c");
            Assert.Equal("a\nb\n", File.ReadAllText(LogPath + ".1"));
            Assert.Equal("c\n", File.ReadAllText(LogPath));
        }

        /// <summary>
        /// Checks a failed initial archive rename keeps the current sink writable without dropping existing
        /// content.
        /// </summary>
        [Fact]
        public void FailedStartupRotationStillAppendsCurrentFile()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(LogPath, "old\n");
            Directory.CreateDirectory(LogPath + ".1");
            using var sink = new RollingLogWriter(LogPath, 2, 3);
            sink.WriteLine("new");
            Assert.Equal("old\nnew\n", File.ReadAllText(LogPath));
        }

        /// <summary>
        /// Checks failed opening does not throw and the next record opens the sink after the filesystem
        /// obstruction is removed.
        /// </summary>
        [Fact]
        public void FailedOpenIsSilentAndCanRecoverOnNextRecord()
        {
            Directory.CreateDirectory(LogPath);
            using var sink = new RollingLogWriter(LogPath, 100, 3);
            sink.WriteLine("unavailable");
            Directory.Delete(LogPath);
            sink.WriteLine("available");
            Assert.Equal("available\n", File.ReadAllText(LogPath));
        }

        /// <summary>
        /// Checks the owned append handle permits live readers and rename, continuing writes through the same
        /// moved file.
        /// </summary>
        [Fact]
        public void PersistentHandleAllowsLiveReadsAndRename()
        {
            using var sink = new RollingLogWriter(LogPath, 100, 3);
            sink.WriteLine("first");
            using (var reader = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                Assert.Equal(6, reader.Length);
                Assert.Equal((byte)'f', reader.ReadByte());
            }

            File.Move(LogPath, LogPath + ".moved");
            sink.WriteLine("second");
            Assert.Equal("first\nsecond\n", File.ReadAllText(LogPath + ".moved"));
            Assert.False(File.Exists(LogPath));
        }

        /// <summary>
        /// Checks repeated disposal and late writes, then proves exclusive file access after handle release.
        /// </summary>
        [Fact]
        public void DisposeIsFinalAndReleasesTheHandle()
        {
            var sink = new RollingLogWriter(LogPath, 100, 3);
            sink.WriteLine("saved");
            sink.Dispose();
            sink.Dispose();
            sink.WriteLine("late");
            using var exclusive = new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(6, exclusive.Length);
        }

        /// <summary>
        /// Checks independent record/archive contents for ordinary, error and nested custom log paths.
        /// </summary>
        [Fact]
        public void MainErrorsAndCustomPathsRotateIndependently()
        {
            var main = Path.Combine(directory, "cthangover.log");
            var errors = Path.Combine(directory, "cthangover_errors.log");
            var custom = Path.Combine(directory, "custom", "capture.txt");
            using var a = new RollingLogWriter(main, 2, 3);
            using var b = new RollingLogWriter(errors, 2, 3);
            using var c = new RollingLogWriter(custom, 2, 3);
            a.WriteLine("a");
            a.WriteLine("b");
            b.WriteLine("x");
            c.WriteLine("c");
            c.WriteLine("d");
            Assert.Equal("a\n", File.ReadAllText(main + ".1"));
            Assert.Equal("b\n", File.ReadAllText(main));
            Assert.Equal("x\n", File.ReadAllText(errors));
            Assert.False(File.Exists(errors + ".1"));
            Assert.Equal("c\n", File.ReadAllText(custom + ".1"));
            Assert.Equal("d\n", File.ReadAllText(custom));
        }

        /// <summary>
        /// Removes this test instance’s temporary directory after all scoped sink/file handles have been
        /// released.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
