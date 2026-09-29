using Cthangover.Core.UI.Tool;
using Godot;

namespace Engine.Tests.Map
{
    /// <summary>
    /// Tests for the pure part of the shared map zone style. The drawing
    /// itself needs a Godot rendering context, but the hatch pattern is the
    /// foundation of the readability guarantee on same-coloured backdrops, so
    /// its geometry is verified here: the pattern must tile seamlessly and
    /// every tile must carry both a light and a dark stripe, otherwise a zone
    /// could again vanish on a matching map patch.
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
        public void HatchPixel_EveryTileContainsBothStripes()
        {
            var light = 0;
            var dark = 0;

            for (var x = 0; x < MapZoneStyle.HatchTile; x++)
            {
                for (var y = 0; y < MapZoneStyle.HatchTile; y++)
                {
                    var pixel = MapZoneStyle.HatchPixel(x, y);
                    if (pixel.A == 0f)
                        continue;

                    if (Luminance(pixel) > 0.5f)
                        light++;
                    else
                        dark++;
                }
            }

            Assert.Equal(MapZoneStyle.HatchTile * MapZoneStyle.HatchStripe, light);
            Assert.Equal(MapZoneStyle.HatchTile * MapZoneStyle.HatchStripe, dark);
        }

        private static float Luminance(Color color)
            => 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
    }
}
