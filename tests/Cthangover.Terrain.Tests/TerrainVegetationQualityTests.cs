namespace Cthangover.TerrainPureTests
{
    using Cthangover.Mods.Terrain;
    using Xunit;
    /// <summary>Tier contracts bound interaction bodies, density, range and update rate.</summary>
    public sealed class TerrainVegetationQualityTests
    {
        [Theory]
        [InlineData("low",.35f,.55f,false,16,.125)]
        [InlineData("medium",.65f,.8f,false,32,.1)]
        [InlineData("high",1f,1f,true,64,.067)]
        public void TierBudgetsAreExplicit(string name,float density,float range,bool shadows,int bodies,double interval)
        {var tier=TerrainVegetationQuality.Resolve(name);Assert.Equal(name,tier.Name);Assert.Equal(density,tier.Density);Assert.Equal(range,tier.Range);Assert.Equal(shadows,tier.Shadows);Assert.Equal(bodies,tier.Bodies);Assert.Equal(interval,tier.Interval);}
    }
}
