using System;
using System.Collections.Generic;
using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    /// <summary>
    /// A save survives composition changes of its game: slots of mods the game
    /// runs without stay dormant and come back with the mod; nothing is dropped.
    /// </summary>
    public class SaveDormancyTests
    {
        private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void LoadedMods_AreLive_MissingMod_GoesDormant()
        {
            var data = new Dictionary<string, ModSlot>
            {
                [SavePackage.CoreSlotKey] = Slot("1.0.0"),
                ["cards"] = Slot("1.0.0"),
                ["recruiting"] = Slot("1.1.0"),
            };

            var result = SaveDormancy.Classify(data, null, Installed(("cards", "1.0.0")), "0.1.0", Now);

            Assert.True(result.Live.ContainsKey("cards"));
            Assert.False(result.Live.ContainsKey(SavePackage.CoreSlotKey));
            var dormant = result.Dormant["recruiting"];
            Assert.True(dormant.Metadata.Dormant);
            Assert.Equal(nameof(SaveDormancy.Reason.NotLoaded), dormant.Metadata.DormantReason);
            Assert.Equal(Now, dormant.Metadata.DormantSince);
            Assert.Contains(("recruiting", SaveDormancy.Reason.NotLoaded), result.NewlyDormant);
        }

        [Fact]
        public void DormantSlot_IsRevived_WhenModReturns()
        {
            var dormant = new Dictionary<string, ModSlot>
            {
                ["recruiting"] = SaveDormancy.MarkDormant(Slot("1.1.0"), SaveDormancy.Reason.NotLoaded, Now.AddDays(-3)),
            };

            var result = SaveDormancy.Classify(new Dictionary<string, ModSlot>(), dormant, Installed(("recruiting", "1.2.0")), "0.1.0", Now);

            Assert.True(result.Live.ContainsKey("recruiting"));
            Assert.False(result.Live["recruiting"].Metadata.Dormant);
            Assert.Null(result.Live["recruiting"].Metadata.DormantSince);
            Assert.Contains("recruiting", result.Revived);
            Assert.Empty(result.Dormant);
        }

        [Fact]
        public void StillMissingDormantSlot_KeepsFirstDormancyDate()
        {
            var since = Now.AddDays(-10);
            var dormant = new Dictionary<string, ModSlot>
            {
                ["recruiting"] = SaveDormancy.MarkDormant(Slot("1.1.0"), SaveDormancy.Reason.NotLoaded, since),
            };

            var result = SaveDormancy.Classify(new Dictionary<string, ModSlot>(), dormant, Installed(), "0.1.0", Now);

            Assert.Equal(since, result.Dormant["recruiting"].Metadata.DormantSince);
            Assert.Empty(result.NewlyDormant);
        }

        [Fact]
        public void Downgrade_KeepsOnlyThatModDormant()
        {
            var data = new Dictionary<string, ModSlot> { ["cards"] = Slot("2.0.0"), ["story"] = Slot("1.0.0") };

            var result = SaveDormancy.Classify(data, null, Installed(("cards", "1.5.0"), ("story", "1.0.0")), "0.1.0", Now);

            Assert.Equal(nameof(SaveDormancy.Reason.Downgrade), result.Dormant["cards"].Metadata.DormantReason);
            Assert.True(result.Live.ContainsKey("story"));
        }

        [Fact]
        public void LiveSlot_WinsOverOlderDormantCopy_ButDormantCopyIsKept()
        {
            var data = new Dictionary<string, ModSlot> { ["cards"] = Slot("1.0.0") };
            var dormant = new Dictionary<string, ModSlot> { ["cards"] = SaveDormancy.MarkDormant(Slot("2.0.0"), SaveDormancy.Reason.Downgrade, Now) };

            var result = SaveDormancy.Classify(data, dormant, Installed(("cards", "1.0.0")), "0.1.0", Now);

            Assert.Same(data["cards"], result.Live["cards"]);
            Assert.Same(dormant["cards"], result.Dormant["cards"]);
        }

        [Fact]
        public void SlotNeedingNewerCore_GoesDormant()
        {
            var slot = Slot("1.0.0");
            slot.Metadata.MinCoreVersion = "2.0.0";

            var result = SaveDormancy.Classify(new Dictionary<string, ModSlot> { ["cards"] = slot }, null, Installed(("cards", "1.0.0")), "0.1.0", Now);

            Assert.Equal(nameof(SaveDormancy.Reason.CoreTooOld), result.Dormant["cards"].Metadata.DormantReason);
        }

        private static ModSlot Slot(string version) => new() { Metadata = new ModMetadata { ModVersion = version } };

        private static Func<string, string> Installed(params (string Id, string Version)[] mods)
        {
            var map = new Dictionary<string, string>();
            foreach (var (id, version) in mods)
                map[id] = version;
            return id => map.TryGetValue(id, out var version) ? version : null;
        }
    }
}
