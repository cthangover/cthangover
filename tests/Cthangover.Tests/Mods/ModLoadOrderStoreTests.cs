using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the persistence behavior of <see cref="ModLoadOrderStore"/> that the
    /// stateless <see cref="ModLoadOrder"/> tests do not cover: the JSON file
    /// itself, the move operations and the cleanup used by
    /// <see cref="ModUninstaller"/>. Uses the default non-runtime path
    /// (<c>mod_load_order.json</c> in the test working directory) and deletes it
    /// after each test.
    /// </summary>
    public class ModLoadOrderStoreTests : IDisposable
    {
        private const string TestFile = "mod_load_order.json";

        public ModLoadOrderStoreTests()
        {
            if (File.Exists(TestFile))
                File.Delete(TestFile);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(TestFile))
                    File.Delete(TestFile);
            }
            catch { }
        }

        [Fact]
        public void Save_ThenLoad_Roundtrip()
        {
            ModLoadOrderStore.Save(new List<string> { "core", "interface", "cooking" });

            var loaded = ModLoadOrderStore.Load();

            Assert.Equal(new List<string> { "core", "interface", "cooking" }, loaded);
        }

        [Fact]
        public void MoveUp_SwapsWithPrevious()
        {
            ModLoadOrderStore.Save(new List<string> { "core", "interface", "cooking" });

            ModLoadOrderStore.MoveUp("interface");

            Assert.Equal(new List<string> { "interface", "core", "cooking" }, ModLoadOrderStore.Load());
        }

        [Fact]
        public void MoveDown_SwapsWithNext()
        {
            ModLoadOrderStore.Save(new List<string> { "core", "interface", "cooking" });

            ModLoadOrderStore.MoveDown("interface");

            Assert.Equal(new List<string> { "core", "cooking", "interface" }, ModLoadOrderStore.Load());
        }

        [Fact]
        public void Remove_ExistingId_DropsItFromSavedOrder()
        {
            ModLoadOrderStore.Save(new List<string> { "core", "interface", "cooking" });

            ModLoadOrderStore.Remove("interface");

            Assert.Equal(new List<string> { "core", "cooking" }, ModLoadOrderStore.Load());
        }

        [Fact]
        public void Remove_UnknownId_LeavesOrderUntouched()
        {
            ModLoadOrderStore.Save(new List<string> { "core", "interface" });

            ModLoadOrderStore.Remove("missing_mod");

            Assert.Equal(new List<string> { "core", "interface" }, ModLoadOrderStore.Load());
        }
    }
}
