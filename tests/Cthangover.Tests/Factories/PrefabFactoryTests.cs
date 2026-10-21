using System.Collections.Generic;
using Cthangover.Core.Factories;

namespace Engine.Tests.Factories
{
    /// <summary>
    /// Сброс кэша фабрики ассетов: после Invalidate/Clear следующий Get читает
    /// "файл" заново (замена картинки во время игры), без сброса — отдаёт
    /// закэшированное. Фабрика-заглушка вместо файлов модов выдаёт для id
    /// текущую "версию файла" из словаря, который тест меняет.
    /// </summary>
    public class PrefabFactoryTests
    {
        private sealed class VersionedFactory : PrefabFactory<string>
        {
            public readonly Dictionary<string, int> Versions = new();

            public VersionedFactory() : base("prefab_factory_test", 8) { }

            public override string GroupName => "test_group";

            protected override List<string> Extensions => new() { ".txt" };

            protected override string ConvertFromBytes(string id, byte[] data, string extension) => null;

            protected override string LoadFromMods(string id, string extension)
                => Versions.TryGetValue(id, out var version) ? $"{id}@{version}" : null;
        }

        [Fact]
        public void Get_FileReplacedWithoutReset_ReturnsCachedValue()
        {
            var factory = new VersionedFactory();
            factory.Versions["bg"] = 1;
            Assert.Equal("bg@1", factory.Get("bg"));

            factory.Versions["bg"] = 2;

            Assert.Equal("bg@1", factory.Get("bg"));
        }

        [Fact]
        public void Invalidate_ReplacedFile_NextGetReadsNewValue()
        {
            var factory = new VersionedFactory();
            factory.Versions["bg"] = 1;
            factory.Versions["other"] = 1;
            factory.Get("bg");
            factory.Get("other");

            factory.Versions["bg"] = 2;
            factory.Versions["other"] = 2;
            factory.Invalidate("bg");

            Assert.Equal("bg@2", factory.Get("bg"));
            Assert.Equal("other@1", factory.Get("other"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("never_loaded")]
        public void Invalidate_UnknownOrEmptyId_KeepsCachedValues(string id)
        {
            var factory = new VersionedFactory();
            factory.Versions["bg"] = 1;
            factory.Get("bg");
            factory.Versions["bg"] = 2;

            factory.Invalidate(id);

            Assert.Equal("bg@1", factory.Get("bg"));
        }

        [Fact]
        public void Clear_ReplacedAndAddedFiles_EveryGetReadsCurrentValue()
        {
            var factory = new VersionedFactory();
            factory.Versions["bg"] = 1;
            factory.Versions["other"] = 1;
            factory.Get("bg");
            factory.Get("other");
            Assert.Null(factory.Get("added"));

            factory.Versions["bg"] = 2;
            factory.Versions["other"] = 3;
            factory.Versions["added"] = 1;
            factory.Clear();

            Assert.Equal("bg@2", factory.Get("bg"));
            Assert.Equal("other@3", factory.Get("other"));
            Assert.Equal("added@1", factory.Get("added"));
        }
    }
}
