namespace Cthangover.Core.Mods
{
    public class ModInfo : IModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public IModFileProvider FileProvider { get; set; }
        public ModManifest Manifest { get; set; }

        public string DisplayTitle => string.IsNullOrEmpty(Name) ? Id : Name;
    }
}
