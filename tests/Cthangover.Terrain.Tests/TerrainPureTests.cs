namespace Cthangover.TerrainPureTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using Cthangover.Mods.Terrain;
    using Godot;
    using Xunit;
    /// <summary>Public-contract tests run individually in the container; the host runs the component as a whole.</summary>
    public sealed class TerrainPureTests : IDisposable
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"cthangover-terrain-unit-"+Guid.NewGuid().ToString("N"));
        [Fact]
        public void DefinitionRoundTripsPreserveEveryFieldAndUnknownData()
        {
            var world=new TerrainWorld{ID="test/world",Extends="test/base",Location="test/location",Material="test/material",RegionCells=128,TileCells=32,PatchCells=16,Spacing=2,MaxLod=2,ViewDistance=500,CollisionRadius=64,Renderer="clipmap",DataBudgetBytes=32*1048576,Regions=new(){new(){Id="r",Asset="test/r",X=-2,Z=3}},Scatter=new(){new(){Id="s",Asset="test/s"}}};
            world.Extra["future"]=JsonSerializer.SerializeToElement(new[]{3,4});world.Regions[0].Extra["future"]=JsonSerializer.SerializeToElement("kept");
            var restored=RoundTrip(world);restored.Validate();Assert.Equal(JsonSerializer.Serialize(world),JsonSerializer.Serialize(restored));Assert.Equal("kept",restored.Regions[0].Extra["future"].GetString());
            var region=new TerrainRegion{ID="r",X=-4,Z=3,Tiles=new(){new(){Id="t",Asset="test/t",X=32,Z=64}}};Assert.Equal(JsonSerializer.Serialize(region),JsonSerializer.Serialize(RoundTrip(region)));
            var tile=new TerrainTile{ID="t",Heights="test/r.raw",Encoding="r32",Origin=-4,Step=.05f,Holes="test/h",Weights="test/w",Density="test/d",Layers=new[]{4,5,6,7}};Assert.Equal(JsonSerializer.Serialize(tile),JsonSerializer.Serialize(RoundTrip(tile)));
            var material=new TerrainMaterial{ID="m",TextureSize=128,UvScale=.23f,TriplanarSlope=.7f,FarDistance=63,Layers=new(){new(){Id="rock",Albedo="a.png",Normal="n.png",Orm="o.png"}}};material.Layers[0].Extra["future"]=JsonSerializer.SerializeToElement(true);Assert.Equal(JsonSerializer.Serialize(material),JsonSerializer.Serialize(RoundTrip(material)));
            var scatter=new TerrainScatter{ID="s",Model="m.glb",Seed=4,Density=.4f,MaxInstances=30,MinSlope=.2f,MaxSlope=.9f,ScaleMin=.3f,ScaleMax=2,MeshDistance=10,EndDistance=40,Wind=.3f,ImpostorSize=64,GroundCover=true,Flexibility=.7f,CollisionRadius=.28f,CollisionHeight=4,MinHeight=-3,MaxHeight=8,MaterialLayers=new[]{1,3}};Assert.Equal(JsonSerializer.Serialize(scatter),JsonSerializer.Serialize(RoundTrip(scatter)));
        }

        [Fact]
        public void LocalSurfaceCoverageRoundTripsOutsideTheTilePalette()
        {
            var material = new TerrainMaterial
            {
                ID = "test/material",
                Layers = new() { new() { Id = "grass" } },
                LocalDetail = new()
                {
                    Mask = "test/mask.png",
                    Bounds = new[] { -12f, -40f, 32f, 64f },
                    Scales = new[] { .35f, .45f, .55f },
                    Layers = new()
                    {
                        new() { Id = "damp_soil", Albedo = "test/soil.png" },
                        new() { Id = "peat", Albedo = "test/peat.png", Normal = "test/peat_normal.png" },
                        new() { Id = "moss", Albedo = "test/moss.png", Orm = "test/moss_orm.png" }
                    }
                }
            };

            var restored = RoundTrip(material);
            TerrainValidation.Material(restored);
            Assert.Equal(JsonSerializer.Serialize(material), JsonSerializer.Serialize(restored));
            Assert.Equal("grass", Assert.Single(restored.Layers).Id);
        }

        private TerrainScatter collisionDefinition;
        [Theory]
        [InlineData(-1,4)]
        [InlineData(0,4)]
        [InlineData(.3f,0)]
        [InlineData(.3f,.5f)]
        [InlineData(float.NaN,4)]
        [InlineData(.3f,float.PositiveInfinity)]
        public void InvalidTrunkDimensionsAreRejected(float radius,float height)
        {
            collisionDefinition=new TerrainScatter{CollisionRadius=radius,CollisionHeight=height};
            Assert.Throws<ArgumentException>(ValidateCollision);
        }
        private void ValidateCollision()=>TerrainScatterPoints.Validate(collisionDefinition);
        [Theory]
        [InlineData(0,0)]
        [InlineData(.3f,.6f)]
        [InlineData(.42f,2.8f)]
        public void OptionalTrunkDimensionsRoundTrip(float radius,float height)
        {
            var restored=RoundTrip(new TerrainScatter{CollisionRadius=radius,CollisionHeight=height});
            TerrainScatterPoints.Validate(restored);
            Assert.Equal(radius,restored.CollisionRadius);
            Assert.Equal(height,restored.CollisionHeight);
        }
        private static T RoundTrip<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        [Fact]
        public void UnsafeInputsAndUnboundedDimensionsAreRejectedBeforeAllocation()
        {
            foreach(var key in new[]{"../height","a/../b","a/%2e%2e/b","a\\b","/height","C:/height","a//b","a\n"})Assert.Throws<ArgumentException>(new InvalidPath(key).Run);
            var world=World();world.RegionCells=300;Assert.Throws<ArgumentException>(world.Validate);world=World();world.MaxLod=30;Assert.Throws<ArgumentException>(world.Validate);world=World();world.Regions.Add(world.Regions[0]);Assert.Throws<ArgumentException>(world.Validate);
            Assert.Throws<InvalidDataException>(InvalidBytes);Assert.Throws<InvalidDataException>(NonfiniteBytes);
        }
        private static void UnsupportedEncoding()=>TerrainReader.Decode(new byte[8],4,"png",0,1,2);
        private static void InvalidBytes()=>TerrainReader.Decode(new byte[3],81,"r16",0,1,9);
        private static void NonfiniteBytes()=>TerrainReader.Decode(BitConverter.GetBytes(float.NaN),1,"r32",0,1,1);
        [Fact]
        public void RawFormatsRetainHeightsAndRejectPngInsteadOfQuantizing()
        {
            var r16=new byte[]{0,0,1,0,255,255,0,128};var values=TerrainReader.Decode(r16,4,"r16",-10,.001f,2);
            Assert.Equal(-10,values[0]);Assert.InRange(values[1],-9.99901f,-9.99899f);Assert.InRange(values[2],55.5349f,55.5351f);
            var r32=new byte[16];for(var i=0;i<4;i++)BitConverter.GetBytes(values[i]).CopyTo(r32,i*4);Assert.Equal(values,TerrainReader.Decode(r32,4,"r32",0,1,2));Assert.Throws<InvalidDataException>(UnsupportedEncoding);
        }
        [Fact]
        public void NegativeBordersAndSeamsUseOneHeightAndHoleRule()
        {
            var data=Data();Assert.Equal(-1,TerrainData.Floor(-1,16));Assert.Equal(-1,TerrainData.Floor(-16,16));Assert.Equal(-2,TerrainData.Floor(-17,16));
            Assert.InRange(data.Height(0,0),-.00001f,.00001f);Assert.InRange(data.Sample(-.5f,-.5f),-.15001f,-.14999f);data.ValidateSeams();
            var bad=Tile(0,0);bad.Heights[0]=900;var second=new TerrainData(World());second.Add(Tile(-16,0));second.Add(bad);Assert.Throws<InvalidDataException>(second.ValidateSeams);
            var weights=Tile(0,0);weights.Weights[0]=1;second=new TerrainData(World());second.Add(Tile(-16,0));second.Add(weights);Assert.Throws<InvalidDataException>(second.ValidateSeams);
        }
        [Fact]
        public void BothSelectorsCoverVisibleInteriorWithoutOverlappingPatches()
        {
            var data=Data();
            foreach(var mode in new[]{"quadtree","clipmap"})
            {
                var patches=TerrainSelection.Select(data,Vector3.Zero,mode);Assert.NotEmpty(patches);
                for(var z=-15;z<16;z+=3)for(var x=-15;x<16;x+=3){var cover=0;foreach(var p in patches)if(x>=p.X&&z>=p.Z&&x<p.X+p.Span&&z<p.Z+p.Span)cover++;Assert.Equal(1,cover);}
            }
        }
        [Fact]
        public void GeneratedGeometryHasSkirtsParentHeightsWeightsAndValidHoleFaces()
        {
            var data=new TerrainData(World());var tile=Tile(0,0);tile.Holes[4+4*17]=1;data.Add(tile);var mesh=TerrainGeometry.Build(data,new TerrainPatchSpec(0,0,8,1));Assert.True(mesh.Vertices.Length>81);Assert.Equal(mesh.Vertices.Length,mesh.Normals.Length);Assert.Equal(mesh.Vertices.Length*4,mesh.Palette.Length);
            Assert.Contains(mesh.Vertices,Skirt);foreach(var index in mesh.Indices)Assert.InRange(index,0,mesh.Vertices.Length-1);
            for(var i=0;i<mesh.Indices.Length;i+=3)for(var j=0;j<3;j++)
            {var index=mesh.Indices[i+j];if(index<81)Assert.NotEqual(new Vector2(4,4),new Vector2(mesh.Vertices[index].X,mesh.Vertices[index].Z));}
            var cancellation=new CancellationToken(true);Assert.Throws<OperationCanceledException>(new CanceledGeometry(data,cancellation).Run);
        }
        private static bool Skirt(Vector3 v)=>v.Y<-.5f;
        [Fact]
        public void DerivedCacheRoundTripCorruptionAndInputInvalidation()
        {
            Directory.CreateDirectory(directory);var cache=new TerrainGeometryCache(directory,"test");var data=Data();var spec=new TerrainPatchSpec(0,0,8,1);var first=cache.Get(data,spec);var second=cache.Get(data,spec);
            Assert.Equal(1,cache.Hits);Assert.Equal(first.Vertices,second.Vertices);Assert.Equal(first.Indices,second.Indices);Assert.Equal(first.Palette,second.Palette);Assert.Equal(first.Normals,second.Normals);Assert.Equal(first.Uv2,second.Uv2);Assert.Equal(first.Weights,second.Weights);
            var path=Directory.GetFiles(directory,"*.bin")[0];File.WriteAllBytes(path,new byte[]{1,2,3});Assert.Null(TerrainGeometryCache.Read(path));cache.Get(data,spec);Assert.Equal(2,cache.Builds);
            data.Hash="different";cache.Get(data,spec);Assert.Equal(3,cache.Builds);
        }
        [Fact]
        public void DerivedCacheBudgetRetainsLeasedFilesAndEvictsOldest()
        {
            Directory.CreateDirectory(directory);var old=Path.Combine(directory,"old.bin");var active=Path.Combine(directory,"active.bin");
            File.WriteAllBytes(old,new byte[100]);File.WriteAllBytes(active,new byte[100]);File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-1));
            using(var lease=TerrainCacheLease.Acquire(old,CancellationToken.None))
            {TerrainCacheBudget.Trim(directory,"*.bin",170,active);Assert.True(File.Exists(old));Assert.True(File.Exists(active));}
            TerrainCacheBudget.Trim(directory,"*.bin",170,active);Assert.False(File.Exists(old));Assert.True(File.Exists(active));
        }
        [Fact]
        public void ScatterIsDeterministicBoundedAndHonorsZeroDensityAndHoles()
        {
            var data=Data();var definition=new TerrainScatter{ID="test/grass",Model="one.glb",Density=.3f,Seed=3,MaxInstances=60};var a=TerrainScatterPoints.Generate(data,definition);var b=TerrainScatterPoints.Generate(data,definition);Assert.NotEmpty(a);Assert.Equal(a,b);Assert.InRange(a.Count,1,60);
            definition.Density=0;Assert.Empty(TerrainScatterPoints.Generate(data,definition));definition.Density=1;definition.MaxInstances=0;Assert.Empty(TerrainScatterPoints.Generate(data,definition));
            definition.MaxInstances=60;foreach(var tile in data.Tiles)Array.Fill(tile.Density,(byte)0);Assert.Empty(TerrainScatterPoints.Generate(data,definition));
        }
        [Fact]
        public void SpeciesMasksUseGlobalLayersAndHeightSlopeAndAreaStayAuthoritative()
        {
            var data=new TerrainData(World());var tile=Tile(0,0);Array.Copy(new[]{4,5,6,7},tile.Layers,4);data.Add(tile);
            var species=new TerrainScatter{Density=2,MaxInstances=10000,MaterialLayers=new[]{4},MinHeight=-1,MaxHeight=6,MaxSlope=.5f};
            var full=TerrainScatterPoints.Generate(data,species);Assert.NotEmpty(full);
            var area=TerrainScatterPoints.GenerateArea(data,species,0,0,8,8);Assert.NotEmpty(area);
            foreach(var item in area){Assert.Contains(item,full);Assert.InRange(item.Origin.X,0,8);Assert.InRange(item.Origin.Z,0,8);}
            species.MaterialLayers=new[]{0};Assert.Empty(TerrainScatterPoints.Generate(data,species));
            species.MaterialLayers=new[]{4};species.MinHeight=20;species.MaxHeight=30;Assert.Empty(TerrainScatterPoints.Generate(data,species));
            species.MinHeight=-1;species.MaxHeight=6;species.MaxSlope=0;Assert.Empty(TerrainScatterPoints.Generate(data,species));
            species.MaxSlope=.5f;Array.Fill(tile.Holes,(byte)1);Assert.Empty(TerrainScatterPoints.Generate(data,species));
        }
        [Fact]
        public void GroundCoverBudgetKeepsTheSamePlacementsAcrossLoadedDatasetExtents()
        {
            var small=new TerrainData(World());small.Add(Tile(0,0));var large=Data();
            var species=new TerrainScatter{GroundCover=true,Density=16,MaxInstances=1000,MeshDistance=10,EndDistance=30,Seed=3};
            var first=TerrainScatterPoints.GenerateArea(small,species,0,0,16,16);var second=TerrainScatterPoints.GenerateArea(large,species,0,0,16,16);
            Assert.NotEmpty(first);Assert.Equal(first,second);Assert.InRange(first.Count,1,species.MaxInstances);
        }
        [Fact]
        public void BulkInstanceBufferPreservesRotationScaleAndCellRelativeOrigin()
        {
            var placement=new Transform3D(Basis.FromEuler(new Vector3(0,1.2f,0)).Scaled(Vector3.One*2),new Vector3(20,3,-10));var cell=new Vector3(16,0,-16);var bytes=TerrainInstanceBuffer.Encode(new[]{placement},Transform3D.Identity,cell);
            Assert.Equal(12,bytes.Length);Assert.Equal(4,bytes[3]);Assert.Equal(3,bytes[7]);Assert.Equal(6,bytes[11]);Assert.Equal(placement.Basis.X.X,bytes[0]);Assert.Equal(placement.Basis.Y.X,bytes[1]);Assert.Equal(placement.Basis.Z.X,bytes[2]);
        }
        private static TerrainWorld World()=>new(){ID="test/world",RegionCells=32,TileCells=16,PatchCells=8,MaxLod=2,ViewDistance=64,Regions=new(){new(){Id="r",Asset="r"}}};
        private static TerrainData Data(){var data=new TerrainData(World()){Hash="fixture"};for(var z=-16;z<=0;z+=16)for(var x=-16;x<=0;x+=16)data.Add(Tile(x,z));return data;}
        private static TerrainTileData Tile(int x,int z)
        {var heights=new float[289];var weights=new byte[1156];var density=new byte[289];for(var i=0;i<289;i++){heights[i]=(x+i%17)*.1f+(z+i/17)*.2f;weights[i*4]=255;density[i]=255;}return new TerrainTileData{Id="t_"+x+"_"+z,X=x,Z=z,Cells=16,Heights=heights,Holes=new byte[289],Weights=weights,Density=density,Layers=new[]{0,1,2,3},Hash="tile"};}
        public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
