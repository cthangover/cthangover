using System;
using System.Text;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for the pose fade math and the part-visibility state machine
    /// against the Cubism SDK 5-r.5 source Framework/src/Effect/CubismPose.cpp
    /// (Phi = 0.5 crossing, back opacity threshold 0.15). The E2E tests run the
    /// real pose update on the native Haru model.
    /// </summary>
    public class CubismPoseFadeTests
    {
        private const float FloatTolerance = 0.0001f;

        /// <summary>Visible opacity grows by deltaTime / fadeTime and clamps at 1.</summary>
        [Fact]
        public void ComputeVisibleOpacity_AddsDeltaTimeRatio()
        {
            float actual = CubismPose.ComputeVisibleOpacity(0.4f, 0.1f, 0.5f);
            Assert.Equal(0.6f, actual, FloatTolerance);
        }

        /// <summary>Visible opacity saturates at 1.</summary>
        [Fact]
        public void ComputeVisibleOpacity_ClampsAtOne()
        {
            Assert.Equal(1f, CubismPose.ComputeVisibleOpacity(1f, 0.5f, 0.5f), FloatTolerance);
            Assert.Equal(1f, CubismPose.ComputeVisibleOpacity(0.9f, 1f, 0.5f), FloatTolerance);
        }

        /**
         * Hidden opacity hand-computed from the SDK piecewise formula:
         * a1 = newOpacity < Phi ? newOpacity * (Phi - 1) / Phi + 1
         *                      : (1 - newOpacity) * Phi / (1 - Phi);
         * backOpacity = (1 - a1) * (1 - newOpacity);
         * if (backOpacity > 0.15) a1 = 1 - 0.15 / (1 - newOpacity);
         */
        [Theory]
        [InlineData(0.25f, 0.8f)]
        [InlineData(0.5f, 0.7f)]
        [InlineData(0.75f, 0.4f)]
        [InlineData(0.9f, 0.1f)]
        [InlineData(1.0f, 0.0f)]
        [InlineData(0.4f, 0.75f)]
        public void ComputeHiddenOpacity_MatchesSdkFormula(float newOpacity, float expected)
        {
            float actual = CubismPose.ComputeHiddenOpacity(newOpacity);
            Assert.Equal(expected, actual, FloatTolerance);
        }

        /// <summary>A synthetic pose file parses without throwing (fade time + groups).</summary>
        [Fact]
        public void Ctor_ParsesSyntheticPose()
        {
            byte[] json = Encoding.UTF8.GetBytes(
                "{ \"FadeInTime\": 0.5, \"Groups\": [ [ { \"Id\": \"PartA\" }, { \"Id\": \"PartB\" } ] ] }");
            var pose = new CubismPose(json);
            Assert.NotNull(pose);
        }

        /// <summary>
        /// E2E on the native Haru model: the first UpdateParameters runs the SDK
        /// Reset — the first group part becomes fully visible, the second hidden.
        /// </summary>
        [Fact]
        public void UpdateParameters_Reset_FirstGroupPartVisible()
        {
            using var model = Live2DTestRuntime.LoadHaruModel();
            var partIds = CubismCoreTestAccess.GetPartIds(model);
            Assert.True(model.GetPartCount() >= 2, "Haru model needs at least two parts");

            byte[] json = Encoding.UTF8.GetBytes("{ \"FadeInTime\": 0.5, \"Groups\": [ ["
                + " { \"Id\": \"" + partIds[0] + "\" },"
                + " { \"Id\": \"" + partIds[1] + "\" } ] ] }");
            var pose = new CubismPose(json);

            pose.UpdateParameters(model, 0.1f);

            float[] opacities = CubismCoreTestAccess.ReadPartOpacities(model);
            Assert.Equal(1f, opacities[0], FloatTolerance);
            Assert.Equal(0f, opacities[1], FloatTolerance);
        }

        /// <summary>Parts referenced through the Link list copy the owner part opacity.</summary>
        [Fact]
        public void UpdateParameters_Link_CopiesOpacity()
        {
            using var model = Live2DTestRuntime.LoadHaruModel();
            var partIds = CubismCoreTestAccess.GetPartIds(model);
            Assert.True(model.GetPartCount() >= 3, "Haru model needs at least three parts");

            byte[] json = Encoding.UTF8.GetBytes("{ \"FadeInTime\": 0.5, \"Groups\": [ ["
                + " { \"Id\": \"" + partIds[0] + "\", \"Link\": [ \"" + partIds[2] + "\" ] },"
                + " { \"Id\": \"" + partIds[1] + "\" } ] ] }");
            var pose = new CubismPose(json);

            pose.UpdateParameters(model, 0.1f);

            float[] opacities = CubismCoreTestAccess.ReadPartOpacities(model);
            Assert.Equal(1f, opacities[0], FloatTolerance);
            Assert.Equal(0f, opacities[1], FloatTolerance);
            Assert.Equal(opacities[0], opacities[2], FloatTolerance);
        }
    }
}
