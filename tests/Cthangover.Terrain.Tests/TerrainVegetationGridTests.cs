namespace Cthangover.TerrainPureTests
{
    using Cthangover.Mods.Terrain;
    using Godot;
    using Xunit;
    /// <summary>Observable transient trails, world scrolling, floor isolation and teleport semantics.</summary>
    public sealed class TerrainVegetationGridTests
    {
        [Fact]
        public void WalkingLeavesRecoveringTrailAndRejectsOtherFloors()
        {
            var grid=new TerrainVegetationGrid();grid.Center(Vector2.Zero);grid.Stamp(new Vector3(-3,2,0),new Vector3(3,2,0),1,1);
            var strength=grid.StrengthAt(Vector2.Zero,2);Assert.True(strength>.5f);Assert.Equal(0,grid.StrengthAt(Vector2.Zero,5));
            grid.Recover(1.6f);Assert.InRange(grid.StrengthAt(Vector2.Zero,2),strength*.3678f,strength*.3680f);
            grid.Recover(20);Assert.Equal(0,grid.StrengthAt(Vector2.Zero,2));
        }
        [Fact]
        public void ScrollingPreservesWorldOverlapAndTeleportDoesNotDrawConnectingTrail()
        {
            var grid=new TerrainVegetationGrid();grid.Center(Vector2.Zero);grid.Stamp(Vector3.Zero,Vector3.Zero,1,1);var strength=grid.StrengthAt(Vector2.Zero,0);
            grid.Center(new Vector2(12,4));Assert.Equal(strength,grid.StrengthAt(Vector2.Zero,0));
            grid.Clear();grid.Stamp(new Vector3(-20,0,0),new Vector3(20,0,0),1,1);Assert.Equal(0,grid.StrengthAt(Vector2.Zero,0));Assert.True(grid.StrengthAt(new Vector2(20,0),0)>.5f);
            grid.Center(new Vector2(1000,1000));Assert.Equal(0,grid.StrengthAt(new Vector2(1000,1000),0));
        }
        [Fact]
        public void LongProjectileSweepsClipToWindowAndClearReleasesAllInfluence()
        {
            var grid=new TerrainVegetationGrid();grid.Center(Vector2.Zero);grid.Sweep(new Vector3(-1000,2,0),new Vector3(1000,2,0),1,.8f);
            Assert.True(grid.StrengthAt(Vector2.Zero,2)>.4f);Assert.Equal(TerrainVegetationGrid.Size*TerrainVegetationGrid.Size*4,grid.Values.Length);
            grid.Clear();Assert.Equal(0,grid.StrengthAt(Vector2.Zero,2));
        }
    }
}
