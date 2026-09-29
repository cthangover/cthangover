using System.Collections.Generic;
using Cthangover.Core.UI.Tool;
using Godot;

namespace Engine.Tests.Map
{
    /// <summary>
    /// Tests for the pure part of the shared map zone style. The drawing
    /// itself needs a Godot rendering context, but the hatch pattern is the
    /// foundation of every zone fill, so its geometry is verified here: the
    /// pattern must tile seamlessly and every tile must carry both accent
    /// colours (gold and orange) in equal measure — a tile that loses one of
    /// the pair would turn the two-tone hatch into a flat wash.
    /// </summary>
    public class MapZoneStyleTests
    {
        [Fact]
        public void HatchPixel_SameDiagonalBand_IsUniform()
        {
            for (var x = 0; x < MapZoneStyle.HatchTile; x++)
            {
                for (var y = 0; y < MapZoneStyle.HatchTile; y++)
                    Assert.Equal(MapZoneStyle.HatchPixel(x, y), MapZoneStyle.HatchPixel(x + 3, y - 3));
            }
        }

        [Fact]
        public void HatchPixel_RepeatsEveryTile()
        {
            for (var x = 0; x < MapZoneStyle.HatchTile; x++)
            {
                for (var y = 0; y < MapZoneStyle.HatchTile; y++)
                {
                    Assert.Equal(MapZoneStyle.HatchPixel(x, y), MapZoneStyle.HatchPixel(x + MapZoneStyle.HatchTile, y));
                    Assert.Equal(MapZoneStyle.HatchPixel(x, y), MapZoneStyle.HatchPixel(x, y + MapZoneStyle.HatchTile));
                }
            }
        }

        [Fact]
        public void HatchPixel_EveryTileContainsBothAccentColours()
        {
            var colors = new List<Color>();
            var counts = new List<int>();

            for (var x = 0; x < MapZoneStyle.HatchTile; x++)
            {
                for (var y = 0; y < MapZoneStyle.HatchTile; y++)
                {
                    var pixel = MapZoneStyle.HatchPixel(x, y);
                    if (pixel.A == 0f)
                        continue;

                    var index = colors.FindIndex(color => color == pixel);
                    if (index < 0)
                    {
                        colors.Add(pixel);
                        counts.Add(1);
                    }
                    else
                    {
                        counts[index]++;
                    }
                }
            }

            Assert.Equal(2, colors.Count);
            Assert.Contains(MapZoneStyle.Gold, colors);
            Assert.Contains(MapZoneStyle.Orange, colors);
            Assert.All(counts, count => Assert.Equal(MapZoneStyle.HatchTile * MapZoneStyle.HatchStripe, count));
        }
    }
}
