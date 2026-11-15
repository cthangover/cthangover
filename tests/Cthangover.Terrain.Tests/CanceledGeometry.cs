namespace Cthangover.TerrainPureTests
{
    using System.Threading;
    using Cthangover.Mods.Terrain;
    public sealed class CanceledGeometry
    {
        private readonly TerrainData data;
        private readonly CancellationToken cancellation;
        public CanceledGeometry(TerrainData data,CancellationToken cancellation){this.data=data;this.cancellation=cancellation;}
        public void Run()=>TerrainGeometry.Build(data,new TerrainPatchSpec(0,0,8,1),cancellation);
    }
}
