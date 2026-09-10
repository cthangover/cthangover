using Cthangover.Core.Mods;

namespace Engine.Tests.Mods.Mocks
{
    public class MockModInfo : IModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }

        public string DisplayTitle =>
            string.IsNullOrEmpty(Name) ? Id : Name;

        public string LocalizedDescription => Description ?? string.Empty;

        public string Version { get; set; }
        public IModFileProvider FileProvider { get; set; }
    }
}
