using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for expression (.exp3.json) parsing and the three blend formulas
    /// against the native Cubism model (Haru.moc3 fixture). The expected values are
    /// computed independently from the SDK 5-r.5 semantics of
    /// CubismExpressionMotion::DoUpdateParameters combined with
    /// CubismModel::Set/Add/MultiplyParameterValue:
    /// <list type="bullet">
    ///   <item>Additive:    clamp(value + expValue * weight)</item>
    ///   <item>Multiply:    clamp(value * (1 + (expValue - 1) * weight))</item>
    ///   <item>Overwrite:   value * (1 - weight) + clamp(expValue) * weight</item>
    /// </list>
    /// The clamp bounds are the model parameter's [min, max] read from the native core.
    /// </summary>
    public class CubismExpressionBlendTests
    {
        private const float FloatTolerance = 0.0001f;

        private const string TestParameterId = "ParamMouthForm";

        // ------------------------------------------------------------------
        // Synthetic expressions on the real Haru model
        // ------------------------------------------------------------------

        /// <summary>Additive with a mid weight adds expValue * weight.</summary>
        [Fact]
        public void Additive_AppliesFormula()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 0.5f, "Add"), TestParameterId, v0: 0.2f, weight: 0.5f);
            Assert.Equal(Clamp(0.2f + 0.5f * 0.5f, GetMin(TestParameterId), GetMax(TestParameterId)), actual, FloatTolerance);
        }

        /// <summary>Additive results clamp to the parameter maximum.</summary>
        [Fact]
        public void Additive_ClampsToParameterMaximum()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 0.5f, "Add"), TestParameterId, v0: 0.95f, weight: 1f);
            Assert.Equal(GetMax(TestParameterId), actual, FloatTolerance);
        }

        /// <summary>Additive results clamp to the parameter minimum.</summary>
        [Fact]
        public void Additive_ClampsToParameterMinimum()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, -0.5f, "Add"), TestParameterId, v0: -0.95f, weight: 1f);
            Assert.Equal(GetMin(TestParameterId), actual, FloatTolerance);
        }

        /// <summary>Additive with weight 1 and zero expression value keeps the value.</summary>
        [Fact]
        public void Additive_WeightOne_ZeroValue_KeepsValue()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 0f, "Add"), TestParameterId, v0: 0.3f, weight: 1f);
            Assert.Equal(0.3f, actual, FloatTolerance);
        }

        /// <summary>Multiply with a mid weight follows value * (1 + (expValue - 1) * weight).</summary>
        [Fact]
        public void Multiply_AppliesFormula()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 1.2f, "Multiply"), TestParameterId, v0: 0.5f, weight: 0.5f);
            Assert.Equal(Clamp(0.5f * (1f + 0.2f * 0.5f), GetMin(TestParameterId), GetMax(TestParameterId)), actual, FloatTolerance);
        }

        /// <summary>Multiply with expValue = 1 is an exact identity.</summary>
        [Fact]
        public void Multiply_ValueOne_IsIdentity()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 1f, "Multiply"), TestParameterId, v0: 0.35f, weight: 0.7f);
            Assert.Equal(0.35f, actual, FloatTolerance);
        }

        /// <summary>Multiply overshoot clamps to the parameter maximum.</summary>
        [Fact]
        public void Multiply_ClampsToParameterMaximum()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 3f, "Multiply"), TestParameterId, v0: 0.5f, weight: 1f);
            Assert.Equal(GetMax(TestParameterId), actual, FloatTolerance);
        }

        /// <summary>Overwrite lerps between the current value and the clamped expression value.</summary>
        [Fact]
        public void Overwrite_AppliesLerp()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, -0.2f, "Overwrite"), TestParameterId, v0: 0.8f, weight: 0.25f);
            float expected = 0.8f * 0.75f + Clamp(-0.2f, GetMin(TestParameterId), GetMax(TestParameterId)) * 0.25f;
            Assert.Equal(expected, actual, FloatTolerance);
        }

        /// <summary>Overwrite clamps the expression value itself, not the lerp result.</summary>
        [Fact]
        public void Overwrite_ClampsExpressionValue()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 99f, "Overwrite"), TestParameterId, v0: 0.6f, weight: 0.5f);
            float expected = 0.6f * 0.5f + GetMax(TestParameterId) * 0.5f;
            Assert.Equal(expected, actual, FloatTolerance);
        }

        /// <summary>Overwrite with weight 1 assigns the clamped expression value.</summary>
        [Fact]
        public void Overwrite_WeightOne_AssignsClampedValue()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, -99f, "Overwrite"), TestParameterId, v0: 0.6f, weight: 1f);
            Assert.Equal(GetMin(TestParameterId), actual, FloatTolerance);
        }

        /// <summary>A missing Blend key behaves as Additive (SDK ExpressionBlendType default).</summary>
        [Fact]
        public void MissingBlend_FallsBackToAdditive()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 0.4f, null), TestParameterId, v0: 0.1f, weight: 1f);
            Assert.Equal(Clamp(0.1f + 0.4f, GetMin(TestParameterId), GetMax(TestParameterId)), actual, FloatTolerance);
        }

        /// <summary>An unknown Blend tag also behaves as Additive (SDK Parse switch default).</summary>
        [Fact]
        public void UnknownBlend_FallsBackToAdditive()
        {
            float actual = ApplySingle(
                ExpJson(TestParameterId, 0.4f, "Weird"), TestParameterId, v0: 0.1f, weight: 1f);
            Assert.Equal(Clamp(0.1f + 0.4f, GetMin(TestParameterId), GetMax(TestParameterId)), actual, FloatTolerance);
        }

        /// <summary>Expression entries targeting unknown parameter ids are silently skipped.</summary>
        [Fact]
        public void UnknownParameterId_IsIgnored()
        {
            float actual = ApplySingle(
                ExpJson("Param__Does__Not__Exist", 1f, "Overwrite"), TestParameterId, v0: 0.42f, weight: 1f);
            Assert.Equal(0.42f, actual, FloatTolerance);
        }

        /// <summary>With several entries for one id the later entry wins (apply order).</summary>
        [Fact]
        public void LastEntryWinsForSameId()
        {
            var entries = new[]
            {
                new { Id = TestParameterId, Value = 0.3f, Blend = "Add" },
                new { Id = TestParameterId, Value = -0.2f, Blend = "Overwrite" },
            };
            string json = JsonSerializer.Serialize(new { FadeInTime = 0.5f, Parameters = entries });

            float actual = ApplySingle(json, TestParameterId, v0: 0f, weight: 1f);
            Assert.Equal(Clamp(-0.2f, GetMin(TestParameterId), GetMax(TestParameterId)), actual, FloatTolerance);
        }

        // ------------------------------------------------------------------
        // Real SDK sample expressions
        // ------------------------------------------------------------------

        /// <summary>F01.exp3.json: Add blend applies with the SDK formula on every entry.</summary>
        [Fact]
        public void RealFixture_F01_Additive()
        {
            ApplyRealFixture("F01.exp3.json", v0: 0f, weight: 1f);
        }

        /// <summary>F04.exp3.json: Multiply blend applies with the SDK formula on every entry.</summary>
        [Fact]
        public void RealFixture_F04_Multiply()
        {
            ApplyRealFixture("F04.exp3.json", v0: 0.8f, weight: 1f);
        }

        /// <summary>F06.exp3.json: Multiply with value 2 exercises the max clamp on eye parameters.</summary>
        [Fact]
        public void RealFixture_F06_MultiplyClamp()
        {
            ApplyRealFixture("F06.exp3.json", v0: 0.5f, weight: 1f);
        }

        /// <summary>
        /// Applies a real exp3.json to the Haru model and checks every listed entry
        /// against the independently computed SDK expectation.
        /// </summary>
        private void ApplyRealFixture(string fileName, float v0, float weight)
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Live2DTestRuntime.SdkResources, "Haru", "expressions", fileName));

            using var document = JsonDocument.Parse(bytes);
            var parameters = document.RootElement.GetProperty("Parameters");

            using var model = Live2DTestRuntime.LoadHaruModel();
            var minimums = CubismCoreTestAccess.ReadParameterMinimums(model);
            var maximums = CubismCoreTestAccess.ReadParameterMaximums(model);
            var values = CubismCoreTestAccess.ReadParameterValues(model);

            // Seed every target parameter with the deterministic start value.
            foreach (var entry in parameters.EnumerateArray())
            {
                int index = CubismCoreTestAccess.FindParameterIndex(model, entry.GetProperty("Id").GetString());
                Assert.True(index >= 0, "Parameter missing in Haru.moc3");
                values[index] = v0;
                CubismCoreTestAccess.WriteParameterValue(model, index, v0);
            }

            var expression = new CubismExpressionMotion(bytes);
            expression.DoUpdateParameters(model, 0f, weight, null);

            foreach (var entry in parameters.EnumerateArray())
            {
                string id = entry.GetProperty("Id").GetString();
                float expValue = entry.GetProperty("Value").GetSingle();
                string blend = entry.TryGetProperty("Blend", out var b) ? b.GetString() : null;
                int index = CubismCoreTestAccess.FindParameterIndex(model, id);
                float actual = CubismCoreTestAccess.ReadParameterValue(model, index);

                float expected;
                switch (blend)
                {
                    case "Multiply":
                        expected = Clamp(v0 * (1f + (expValue - 1f) * weight), minimums[index], maximums[index]);
                        break;
                    case "Overwrite":
                        expected = v0 * (1f - weight) + Clamp(expValue, minimums[index], maximums[index]) * weight;
                        break;
                    default: // "Add" and the SDK default
                        expected = Clamp(v0 + expValue * weight, minimums[index], maximums[index]);
                        break;
                }

                Assert.True(
                    MathF.Abs(expected - actual) <= FloatTolerance,
                    $"{fileName} [{id}]: expected {expected}, got {actual}");
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Builds a minimal .exp3.json with a single parameter entry.</summary>
        private static string ExpJson(string id, float value, string blend)
        {
            var blendPart = blend == null ? string.Empty : ", \"Blend\": \"" + blend + "\"";
            return "{ \"FadeInTime\": 0.5, \"Parameters\": [ { \"Id\": \"" + id
                + "\", \"Value\": " + value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + blendPart + " } ] }";
        }

        private static float GetMin(string id) => LoadBounds(id).min;

        private static float GetMax(string id) => LoadBounds(id).max;

        private static (float min, float max) LoadBounds(string id)
        {
            using var model = Live2DTestRuntime.LoadHaruModel();
            int index = CubismCoreTestAccess.FindParameterIndex(model, id);
            Assert.True(index >= 0, id + " not found in Haru.moc3");
            return (CubismCoreTestAccess.ReadParameterMinimums(model)[index],
                    CubismCoreTestAccess.ReadParameterMaximums(model)[index]);
        }

        /// <summary>
        /// Loads the Haru model, seeds the target parameter with v0, applies the
        /// expression with the given fade weight and returns the parameter value.
        /// </summary>
        private static float ApplySingle(string expJson, string targetId, float v0, float weight)
        {
            using var model = Live2DTestRuntime.LoadHaruModel();
            int index = CubismCoreTestAccess.FindParameterIndex(model, targetId);
            Assert.True(index >= 0, targetId + " not found in Haru.moc3");
            CubismCoreTestAccess.WriteParameterValue(model, index, v0);

            var expression = new CubismExpressionMotion(Encoding.UTF8.GetBytes(expJson));
            expression.DoUpdateParameters(model, 0f, weight, null);

            return CubismCoreTestAccess.ReadParameterValue(model, index);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
