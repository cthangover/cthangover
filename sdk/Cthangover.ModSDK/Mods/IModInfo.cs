namespace Cthangover.Core.Mods
{
    public interface IModInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Description { get; }
        public string DisplayTitle { get; }
        public IModFileProvider FileProvider { get; }
    }
}
