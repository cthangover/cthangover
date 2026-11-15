namespace Cthangover.TerrainPureTests
{
    using Cthangover.Tools.Terrain;
    /// <summary>Named callback keeps exception checks free of anonymous functions.</summary>
    public sealed class TerrainImportRejection
    {
        public byte[] Bytes {get;set;}
        public float Origin {get;set;}
        public float Step {get;set;}=1;
        public void Run()=>TerrainHeightImport.Png16(Bytes,Origin,Step);
    }
}
