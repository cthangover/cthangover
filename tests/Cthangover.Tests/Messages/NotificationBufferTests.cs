using Cthangover.Core.UI.Messages;

namespace Engine.Tests.Messages
{
    public class NotificationBufferTests
    {
        public NotificationBufferTests()
        {
            NotificationBuffer.Clear();
        }

        // ── Add ──

        [Fact]
        public void Add_ValidText_EnqueuesAndIncreasesPendingCount()
        {
            NotificationBuffer.Add("Test notification");
            Assert.Equal(1, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void Add_MultipleTexts_AllQueued()
        {
            NotificationBuffer.Add("First");
            NotificationBuffer.Add("Second");
            NotificationBuffer.Add("Third");
            Assert.Equal(3, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void Add_NullText_IsIgnored()
        {
            NotificationBuffer.Add(null);
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void Add_EmptyString_IsIgnored()
        {
            NotificationBuffer.Add("");
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void Add_WhitespaceString_IsAccepted()
        {
            NotificationBuffer.Add("   ");
            Assert.Equal(1, NotificationBuffer.PendingCount);
        }

        // ── TryDequeue ──

        [Fact]
        public void TryDequeue_BufferHasEntry_ReturnsTrueAndText()
        {
            NotificationBuffer.Add("Hello");
            var result = NotificationBuffer.TryDequeue(out var text);

            Assert.True(result);
            Assert.Equal("Hello", text);
        }

        [Fact]
        public void TryDequeue_BufferEmpty_ReturnsFalseAndNullText()
        {
            var result = NotificationBuffer.TryDequeue(out var text);

            Assert.False(result);
            Assert.Null(text);
        }

        [Fact]
        public void TryDequeue_PreservesFifoOrder()
        {
            NotificationBuffer.Add("A");
            NotificationBuffer.Add("B");
            NotificationBuffer.Add("C");

            NotificationBuffer.TryDequeue(out var first);
            NotificationBuffer.TryDequeue(out var second);
            NotificationBuffer.TryDequeue(out var third);

            Assert.Equal("A", first);
            Assert.Equal("B", second);
            Assert.Equal("C", third);
        }

        [Fact]
        public void TryDequeue_AfterDequeue_DecreasesPendingCount()
        {
            NotificationBuffer.Add("A");
            NotificationBuffer.Add("B");

            NotificationBuffer.TryDequeue(out _);
            Assert.Equal(1, NotificationBuffer.PendingCount);

            NotificationBuffer.TryDequeue(out _);
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        // ── PendingCount ──

        [Fact]
        public void PendingCount_EmptyBuffer_ReturnsZero()
        {
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void PendingCount_AfterClear_ReturnsZero()
        {
            NotificationBuffer.Add("A");
            NotificationBuffer.Add("B");
            NotificationBuffer.Clear();
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        [Fact]
        public void PendingCount_AfterDequeueAll_ReturnsZero()
        {
            NotificationBuffer.Add("A");
            NotificationBuffer.TryDequeue(out _);
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }

        // ── Clear ──

        [Fact]
        public void Clear_WithPendingItems_EmptiesBuffer()
        {
            NotificationBuffer.Add("A");
            NotificationBuffer.Add("B");
            NotificationBuffer.Clear();

            Assert.Equal(0, NotificationBuffer.PendingCount);
            Assert.False(NotificationBuffer.TryDequeue(out _));
        }

        [Fact]
        public void Clear_OnEmptyBuffer_DoesNotThrow()
        {
            NotificationBuffer.Clear();
            Assert.Equal(0, NotificationBuffer.PendingCount);
        }
    }
}
