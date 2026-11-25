namespace Cthangover.Tests.Mods.HotReload
{
    using Cthangover.Core.Mods.HotReload;

    /// <summary>Ignored file changes must never become an automatic reload or a deferred permission.</summary>
    public sealed class ModReloadPromptStateTests
    {
        [Fact]
        public void Offer_ExpiresWithoutGrantingReload()
        {
            var state = new ModReloadPromptState();
            Assert.False(state.IsVisible(0));
            state.Offer(10);
            Assert.True(state.IsVisible(129.999));
            Assert.False(state.IsVisible(130), "Two minutes of real time must dismiss an ignored offer");
            Assert.False(state.TryConsume(131), "An expired offer cannot authorize a delayed reload");
        }

        [Fact]
        public void Click_ConsumesOnlyOnePendingOffer()
        {
            var state = new ModReloadPromptState();
            state.Offer(20);
            Assert.True(state.TryConsume(21));
            Assert.False(state.TryConsume(21), "Repeated button signals must not start a second reload");
            Assert.False(state.IsVisible(22));
        }

        [Fact]
        public void NewChange_AfterExpiryGetsItsOwnWindow()
        {
            var state = new ModReloadPromptState();
            state.Offer(0);
            Assert.False(state.IsVisible(121));
            state.Offer(200);
            Assert.True(state.IsVisible(319));
            state.Dismiss();
            Assert.False(state.TryConsume(319), "Keyboard reload dismisses the old button without another action");
        }
    }
}
