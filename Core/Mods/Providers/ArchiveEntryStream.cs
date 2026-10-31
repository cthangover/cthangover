using System;
using System.IO;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// Transfers an entry decoder and its archive to the caller as one stream.
    /// Each 7z read owns a separate archive, so partial, interleaved and parallel reads
    /// cannot share a solid-block decoder or move each other's underlying file cursor.
    /// </summary>
    internal sealed class ArchiveEntryStream : Stream
    {
        private readonly Stream inner;
        private readonly IDisposable archive;
        private bool disposed;

        internal ArchiveEntryStream(Stream inner, IDisposable archive)
        {
            this.inner = inner;
            this.archive = archive;
        }

        /// <inheritdoc />
        public override bool CanRead => !disposed && inner.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => !disposed && inner.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override long Length => inner.Length;
        /// <inheritdoc />
        public override long Position { get => inner.Position; set => inner.Position = value; }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        /// <inheritdoc />
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        /// <inheritdoc />
        public override void Flush() => inner.Flush();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                try { inner.Dispose(); }
                finally { archive.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
