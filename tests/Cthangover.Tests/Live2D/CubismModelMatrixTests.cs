using System.Collections.Generic;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for CubismModelMatrix against the Cubism SDK 5-r.5 source
    /// Framework/src/Math/CubismModelMatrix.cpp. Key semantics under test:
    /// SetWidth/SetHeight MULTIPLY the scale, the (width, height) constructor
    /// performs SetHeight(2), translation helpers are incremental, and the
    /// alignment helpers offset by the full scaled canvas size.
    /// </summary>
    public class CubismModelMatrixTests
    {
        private const float FloatTolerance = 0.0001f;

        /// <summary>The default constructor is the identity transform.</summary>
        [Fact]
        public void DefaultCtor_IsIdentity()
        {
            var matrix = new CubismModelMatrix();

            Assert.Equal(1f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(1f, matrix.ScaleY, FloatTolerance);
            Assert.Equal(0f, matrix.TranslateX, FloatTolerance);
            Assert.Equal(0f, matrix.TranslateY, FloatTolerance);
        }

        /// <summary>The canvas constructor calls SetHeight(2), which for a 2-high canvas yields scale 1.</summary>
        [Fact]
        public void CanvasCtor_AppliesSetHeightTwo()
        {
            var matrix = new CubismModelMatrix(4f, 2f);

            Assert.Equal(1f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(1f, matrix.ScaleY, FloatTolerance);
        }

        /// <summary>SetWidth multiplies the current scale (SDK Scale semantics).</summary>
        [Fact]
        public void SetWidth_MultipliesScale()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.SetWidth(2f); // scale *= 2/4
            Assert.Equal(0.5f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(0.5f, matrix.ScaleY, FloatTolerance);

            matrix.SetWidth(4f); // scale *= 4/4 — no change, proving multiply not assign
            Assert.Equal(0.5f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(0.5f, matrix.ScaleY, FloatTolerance);
        }

        /// <summary>SetHeight multiplies the current scale.</summary>
        [Fact]
        public void SetHeight_MultipliesScale()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.SetHeight(1f); // scale *= 1/2
            Assert.Equal(0.5f, matrix.ScaleY, FloatTolerance);
            Assert.Equal(0.5f, matrix.ScaleX, FloatTolerance);
        }

        /// <summary>An unknown canvas size makes SetWidth/SetHeight no-ops (SDK divide guard).</summary>
        [Fact]
        public void UnknownCanvas_IgnoresWidthHeight()
        {
            var matrix = new CubismModelMatrix();
            matrix.SetWidth(3f);
            matrix.SetHeight(3f);

            Assert.Equal(1f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(1f, matrix.ScaleY, FloatTolerance);
        }

        /// <summary>Top/Left are incremental translations (SDK SetY/SetX add).</summary>
        [Fact]
        public void TopAndLeft_AreIncremental()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.Top(1f);
            matrix.Top(1f);
            matrix.Left(0.5f);
            matrix.Left(0.5f);

            Assert.Equal(2f, matrix.TranslateY, FloatTolerance);
            Assert.Equal(1f, matrix.TranslateX, FloatTolerance);
        }

        /// <summary>Bottom offsets by y minus the scaled canvas height, incrementally.</summary>
        [Fact]
        public void Bottom_UsesScaledCanvasHeight()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.Bottom(0f); // translateY += 0 - (2 * 1)
            Assert.Equal(-2f, matrix.TranslateY, FloatTolerance);
            matrix.Bottom(0f); // second call adds again
            Assert.Equal(-4f, matrix.TranslateY, FloatTolerance);
        }

        /// <summary>Right offsets by x minus the scaled canvas width.</summary>
        [Fact]
        public void Right_UsesScaledCanvasWidth()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.Right(0f);
            Assert.Equal(-4f, matrix.TranslateX, FloatTolerance);
        }

        /// <summary>CenterX/CenterY offset by the point minus half the scaled canvas size.</summary>
        [Fact]
        public void Center_UsesHalfScaledCanvas()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.CenterX(0f);
            matrix.CenterY(0f);

            Assert.Equal(-2f, matrix.TranslateX, FloatTolerance);
            Assert.Equal(-1f, matrix.TranslateY, FloatTolerance);
        }

        /// <summary>SetPosition adds to the current translation.</summary>
        [Fact]
        public void SetPosition_IsIncremental()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.SetPosition(1f, 2f);
            matrix.SetPosition(1f, 2f);

            Assert.Equal(2f, matrix.TranslateX, FloatTolerance);
            Assert.Equal(4f, matrix.TranslateY, FloatTolerance);
        }

        /// <summary>SetCenterPosition centers on both axes.</summary>
        [Fact]
        public void SetCenterPosition_CombinesCenterXAndY()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.SetCenterPosition(0f, 0f);

            Assert.Equal(-2f, matrix.TranslateX, FloatTolerance);
            Assert.Equal(-1f, matrix.TranslateY, FloatTolerance);
        }

        /// <summary>
        /// SetupFromLayout applies width/height (scale) before the position keys,
        /// and matches the lowercased SDK layout keys.
        /// </summary>
        [Fact]
        public void SetupFromLayout_AppliesScaleThenPosition()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            var layout = new Dictionary<string, float>
            {
                { "width", 8f },  // scale *= 8/4
                { "y", 1f },
                { "center_x", 0f }, // translateX += 0 - (4 * 2)/2 = -4
            };
            matrix.SetupFromLayout(layout);

            Assert.Equal(2f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(2f, matrix.ScaleY, FloatTolerance);
            Assert.Equal(1f, matrix.TranslateY, FloatTolerance);
            Assert.Equal(-4f, matrix.TranslateX, FloatTolerance);
        }

        /// <summary>Scale multiplies both axes directly.</summary>
        [Fact]
        public void Scale_MultipliesBothAxes()
        {
            var matrix = new CubismModelMatrix(4f, 2f);
            matrix.Scale(0.5f, 2f);

            Assert.Equal(0.5f, matrix.ScaleX, FloatTolerance);
            Assert.Equal(2f, matrix.ScaleY, FloatTolerance);
        }
    }
}
