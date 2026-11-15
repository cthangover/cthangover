namespace Cthangover.TerrainPureTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using Cthangover.Mods.Terrain;
    using Cthangover.Tools.Terrain;
    using Xunit;
    /// <summary>Portable editor contracts exercise exact imported data, shared-border edits, whole-stroke history and real interrupted file transactions.</summary>
    public sealed class TerrainEditorPureTests : IDisposable
    {
        private TerrainUndoStore pendingHistory;
        private void EndHistory()=>pendingHistory.End();
        private readonly string directory=Path.Combine(Path.GetTempPath(),"cthangover-terrain-editor-test-"+Guid.NewGuid().ToString("N"));
        [Fact]
        public void Png16RetainsAll65536CodesAndR32SaveRetainsTheirMetres()
        {
            var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/all_codes.png"));var image=TerrainHeightImport.Png16(bytes,-16,.001f);Assert.Equal(256,image.Width);Assert.Equal(256,image.Height);Assert.Equal(65536,image.Values.Length);
            for(var i=0;i<65536;i++){Assert.Equal((ushort)i,image.Codes[i]);Assert.Equal(-16+i*.001f,image.Values[i]);}
            var payload=TerrainTileBytes.Heights(new TerrainTileData{Heights=image.Values});var reopened=TerrainHeightImport.Raw(payload,256,256,"r32",0,1);Assert.Equal(image.Values,reopened.Values);
        }
        [Fact]
        public void ImportRejectsPng8AndCorruptOrOutOfRangeSources()
        {
            var fixture=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/eight_bit.png"));var request=new TerrainImportRejection{Bytes=fixture};Assert.Throws<InvalidDataException>(request.Run);
            fixture=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/all_codes.png"));fixture[40]^=1;request.Bytes=fixture;Assert.Throws<InvalidDataException>(request.Run);
            request.Bytes=fixture.AsSpan(0,35).ToArray();Assert.Throws<InvalidDataException>(request.Run);
        }
        [Fact]
        public void ImportRejectsMetreMappingsThatCollapseAdjacentCodesInsideTheRange()
        {
            var request=new TerrainImportRejection{Bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures/all_codes.png")),Origin=10000,Step=.0006f};
            Assert.Throws<InvalidDataException>(request.Run);
        }
        [Fact]
        public void OneStrokeAcrossFourTilesUndoesEveryDabAndLeavesInputUntouched()
        {
            var input=Data();using var world=new TerrainEditableWorld(input,Path.Combine(directory,"undo"));var before=TerrainTileBytes.Fingerprint(input.Tiles);var brush=new TerrainBrush{Radius=3,Strength=2};world.BeginStroke();brush.Dab(world,0,0);brush.Dab(world,1,0);world.EndStroke();
            Assert.Equal(1,world.History.UndoCount);Assert.True(world.Data.Height(0,0)>0);world.Data.ValidateSeams();Assert.Equal(before,TerrainTileBytes.Fingerprint(input.Tiles));
            Assert.True(world.Undo());Assert.Equal(0,world.Data.Height(0,0));world.Data.ValidateSeams();Assert.Equal(before,TerrainTileBytes.Fingerprint(world.Tiles.Values));
            Assert.True(world.Redo());Assert.True(world.Data.Height(0,0)>0);world.Data.ValidateSeams();
        }
        [Fact]
        public void BrushesPreserveNormalizedWeightsHoleAndDensityBorders()
        {
            using var world=new TerrainEditableWorld(Data(),Path.Combine(directory,"undo"));Apply(world,new TerrainBrush{Mode="raise",Radius=.5f,Strength=2});Assert.Equal(2,world.Data.Height(0,0));
            Apply(world,new TerrainBrush{Mode="lower",Radius=.5f,Strength=1});Assert.Equal(1,world.Data.Height(0,0));
            Apply(world,new TerrainBrush{Mode="flatten",Radius=.5f,Strength=1,TargetHeight=7});Assert.Equal(7,world.Data.Height(0,0));
            Apply(world,new TerrainBrush{Mode="smooth",Radius=.5f,Strength=1});Assert.Equal(0,world.Data.Height(0,0));
            Apply(world,new TerrainBrush{Mode="material",Radius=.5f,Strength=1,Layer=2});var value=world.Point(0,0);Assert.Equal(255,value.W2);Assert.Equal(255,value.W0+value.W1+value.W2+value.W3);
            Apply(world,new TerrainBrush{Mode="holes",Radius=.5f,Strength=1});Assert.True(world.Data.Hole(0,0));
            Apply(world,new TerrainBrush{Mode="holes",Radius=.5f,Strength=1,Erase=true});Assert.False(world.Data.Hole(0,0));
            Apply(world,new TerrainBrush{Mode="density",Radius=.5f,Strength=1,Erase=true});Assert.Equal(0,world.Data.Density(0,0));
            Apply(world,new TerrainBrush{Mode="density",Radius=.5f,Strength=1});Assert.Equal(255,world.Data.Density(0,0));world.Data.ValidateSeams();
        }
        [Fact]
        public void SpeciesBrushRestoresDensityAndMasksAcrossSeamsInOneUndo()
        {
            var input=Data();using var world=new TerrainEditableWorld(input,Path.Combine(directory,"species"));var before=TerrainTileBytes.Fingerprint(world.Tiles.Values);
            var brush=new TerrainBrush{Mode="vegetation",SpeciesLayer=2,Density=.7f,Radius=3,Strength=1,Falloff=0};world.BeginStroke();brush.Dab(world,0,0);brush.Dab(world,2,0);world.EndStroke();
            var after=TerrainTileBytes.Fingerprint(world.Tiles.Values);Assert.NotEqual(before,after);Assert.Equal(1,world.History.UndoCount);Assert.Equal(255,world.Point(0,0).W2);Assert.Equal(178,world.Point(0,0).Density);world.Data.ValidateSeams();
            Assert.True(world.Undo());Assert.Equal(before,TerrainTileBytes.Fingerprint(world.Tiles.Values));Assert.True(world.Redo());Assert.Equal(after,TerrainTileBytes.Fingerprint(world.Tiles.Values));
            brush.Erase=true;Apply(world,brush);Assert.Equal(0,world.Point(0,0).W2);Assert.Equal(178,world.Point(0,0).Density);Assert.True(world.Undo());Assert.Equal(after,TerrainTileBytes.Fingerprint(world.Tiles.Values));Assert.Equal(before,TerrainTileBytes.Fingerprint(input.Tiles));
        }
        [Fact]
        public void ResetRetainsExactPerTileValuesWhenEncodingsDifferWithinSeamTolerance()
        {
            var input=Data();foreach(var tile in input.Tiles)if(tile.X==0&&tile.Z==0)tile.Heights[0]=.000001f;input.ValidateSeams();
            using var world=new TerrainEditableWorld(input,Path.Combine(directory,"undo"));var baseline=TerrainTileBytes.Fingerprint(input.Tiles);Apply(world,new TerrainBrush{Radius=2,Strength=3});var edited=TerrainTileBytes.Fingerprint(world.Tiles.Values);
            world.Reset(input);Assert.Equal(baseline,TerrainTileBytes.Fingerprint(world.Tiles.Values));world.Data.ValidateSeams();Assert.True(world.Undo());Assert.Equal(edited,TerrainTileBytes.Fingerprint(world.Tiles.Values));
        }
        [Fact]
        public void MaskPaintingDoesNotRewriteUneditedHeightSamplesAtMixedEncodingSeams()
        {
            var input=Data();foreach(var tile in input.Tiles)if(tile.X==0&&tile.Z==0)tile.Heights[0]=.000001f;
            using var world=new TerrainEditableWorld(input,Path.Combine(directory,"undo"));Apply(world,new TerrainBrush{Mode="holes",Radius=1,Strength=1});Apply(world,new TerrainBrush{Mode="density",Radius=1,Strength=1});Apply(world,new TerrainBrush{Mode="material",Layer=2,Radius=1,Strength=1});
            var originals=new Dictionary<string,float[]>();foreach(var tile in input.Tiles)originals[tile.Id]=tile.Heights;foreach(var tile in world.Tiles.Values)Assert.Equal(originals[tile.Id],tile.Heights);world.Data.ValidateSeams();
        }
        [Fact]
        public void RejectedHistoryBudgetRestoresDataAndCompleteHistoryIsDiskBounded()
        {
            var tile=Tile(0,0);using var store=new TerrainUndoStore(Path.Combine(directory,"history"),1);store.Begin();store.Touch(tile,0,0);tile.Heights[0]=6;pendingHistory=store;Assert.Throws<IOException>(EndHistory);Assert.Equal(0,tile.Heights[0]);Assert.Equal(0,store.UndoCount);Assert.False(store.Active);
        }
        [Fact]
        public void InterruptedSaveRestoresAllBeforeBytesAndCommitSurvivesReopen()
        {
            Directory.CreateDirectory(directory);var old=Encoding.UTF8.GetBytes("old payload");File.WriteAllBytes(Path.Combine(directory,"height.r32"),old);File.WriteAllText(Path.Combine(directory,"tile.json"),"before");File.WriteAllText(Path.Combine(directory,"untouched"),"keep");
            var writes=new Dictionary<string,byte[]>{{"height.r32",Encoding.UTF8.GetBytes("new payload")},{"new/mask",new byte[]{1,2,3}},{"tile.json",Encoding.UTF8.GetBytes("after")}};
            using(var partial=TerrainSaveTransaction.Prepare(directory,writes)){Assert.True(partial.PublishNext());Assert.True(partial.PublishNext());var copy=JsonSerializer.Deserialize<TerrainSaveJournal>(JsonSerializer.Serialize(partial.Journal));Assert.Equal(JsonSerializer.Serialize(partial.Journal),JsonSerializer.Serialize(copy));}
            Assert.Equal(1,TerrainSaveTransaction.Recover(directory));Assert.Equal(old,File.ReadAllBytes(Path.Combine(directory,"height.r32")));Assert.Equal("before",File.ReadAllText(Path.Combine(directory,"tile.json")));Assert.False(File.Exists(Path.Combine(directory,"new/mask")));Assert.Equal("keep",File.ReadAllText(Path.Combine(directory,"untouched")));
            TerrainSaveTransaction.Execute(directory,writes);Assert.Equal("after",File.ReadAllText(Path.Combine(directory,"tile.json")));Assert.Equal(0,TerrainSaveTransaction.Recover(directory));Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(Path.Combine(directory,"new/mask")));
            TerrainSaveTransaction.Execute(directory,new Dictionary<string,byte[]>{{"tile.json",null}});Assert.False(File.Exists(Path.Combine(directory,"tile.json")));
        }
        private static void Apply(TerrainEditableWorld world,TerrainBrush brush){world.BeginStroke();brush.Dab(world,0,0);world.EndStroke();}
        private static TerrainData Data(){var definition=new TerrainWorld{ID="test/world",RegionCells=32,TileCells=16,PatchCells=8,Regions=new(){new(){Id="r",Asset="r"}}};var data=new TerrainData(definition){Hash="editor-test"};for(var z=-16;z<=0;z+=16)for(var x=-16;x<=0;x+=16)data.Add(Tile(x,z));return data;}
        private static TerrainTileData Tile(int x,int z)
        {var weights=new byte[289*4];for(var i=0;i<289;i++)weights[i*4]=255;return new TerrainTileData{Id="tile_"+x+"_"+z,X=x,Z=z,Cells=16,Heights=new float[289],Holes=new byte[289],Weights=weights,Density=new byte[289],Layers=new[]{0,1,2,3},Hash="flat"};}
        public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
