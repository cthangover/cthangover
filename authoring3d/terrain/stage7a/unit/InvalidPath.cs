namespace Cthangover.TerrainPureTests
{
    using Cthangover.Mods.Terrain;
    public sealed class InvalidPath
    {
        private readonly string path;
        public InvalidPath(string path){this.path=path;}
        public void Run()=>TerrainValidation.Path(path);
    }
}
