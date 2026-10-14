using System;
using System.IO;
using System.Text.Json;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for the pure physics math core against independent transcriptions
    /// of the Cubism SDK 5-r.5 C++ sources (Framework/src/Physics/CubismPhysics.cpp
    /// and Framework/src/Math/CubismMath.cpp). The particle update deliberately
    /// reproduces the native quirk that the Y component is rotated using the
    /// already-updated X (bug-compatible behavior preserved by the mod).
    /// </summary>
    public class CubismPhysicsMathTests
    {
        private const float FloatTolerance = 0.0001f;

        /// <summary>SDK CubismPhysics.cpp anonymous namespace constants.</summary>
        private const float AirResistance = 5f;
        private const float MaximumWeight = 100f;

        // ------------------------------------------------------------------
        // NormalizeParameterValue (CubismPhysics.cpp lines 73-142)
        // ------------------------------------------------------------------

        /// <summary>Above-default value, non-inverted input: the result is negated.</summary>
        [Fact]
        public void NormalizeParameterValue_PositiveBranch_NotInvertedIsNegated()
        {
            float actual = CubismPhysics.NormalizeParameterValue(
                1f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(-1f, actual, FloatTolerance);
        }

        /// <summary>Below-default value, non-inverted input.</summary>
        [Fact]
        public void NormalizeParameterValue_NegativeBranch()
        {
            float actual = CubismPhysics.NormalizeParameterValue(
                -0.5f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(0.5f, actual, FloatTolerance);
        }

        /// <summary>Value at the parameter middle maps onto the normalized default.</summary>
        [Fact]
        public void NormalizeParameterValue_AtDefault_MapsToNormalizedDefault()
        {
            float actual = CubismPhysics.NormalizeParameterValue(
                0f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(0f, actual, FloatTolerance);
        }

        /// <summary>The input value clamps to the parameter range before mapping.</summary>
        [Fact]
        public void NormalizeParameterValue_ClampsToParameterRange()
        {
            float above = CubismPhysics.NormalizeParameterValue(
                100f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            float atMax = CubismPhysics.NormalizeParameterValue(
                1f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(atMax, above, FloatTolerance);

            float below = CubismPhysics.NormalizeParameterValue(
                -100f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            float atMin = CubismPhysics.NormalizeParameterValue(
                -1f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(atMin, below, FloatTolerance);
        }

        /// <summary>Inverted inputs skip the native sign flip (C++ lines 140-142).</summary>
        [Fact]
        public void NormalizeParameterValue_Inverted_SkipsSignFlip()
        {
            float actual = CubismPhysics.NormalizeParameterValue(
                1f, -1f, 1f, 0f, -1f, 1f, 0f, isInverted: true);
            Assert.Equal(1f, actual, FloatTolerance);
        }

        /// <summary>Asymmetric parameter ranges scale the piecewise segments proportionally.</summary>
        [Fact]
        public void NormalizeParameterValue_AsymmetricRanges()
        {
            // Parameter [0, 10] with default middle 5, normalized [-1, 1] default 0.
            float upper = CubismPhysics.NormalizeParameterValue(
                7.5f, 0f, 10f, 5f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(-0.5f, upper, FloatTolerance);

            float lower = CubismPhysics.NormalizeParameterValue(
                2.5f, 0f, 10f, 5f, -1f, 1f, 0f, isInverted: false);
            Assert.Equal(0.5f, lower, FloatTolerance);
        }

        // ------------------------------------------------------------------
        // UpdateParticles (CubismPhysics.cpp lines 277-352)
        // ------------------------------------------------------------------

        /// <summary>
        /// A four-particle strand follows the C++ transcription step by step,
        /// including the bug-compatible rotation order (Y computed from the
        /// updated X) and the velocity update divided by the delay.
        /// </summary>
        [Fact]
        public void UpdateParticles_MatchesCppTranscription()
        {
            var modStrand = MakeStrand();
            var referenceStrand = MakeStrand();

            for (int step = 1; step <= 3; step++)
            {
                CubismPhysics.UpdateParticles(
                    modStrand, 0, modStrand.Length,
                    0.1f, -0.05f, 30f, 0.2f, 0.1f, 0.001f, 1f / 30f, AirResistance);

                PhysicsTranscription.UpdateParticles(
                    referenceStrand, 0, referenceStrand.Length,
                    0.1f, -0.05f, 30f, 0.2f, 0.1f, 0.001f, 1f / 30f, AirResistance);

                AssertStrandsEqual(modStrand, referenceStrand, $"step {step}");
            }
        }

        /// <summary>After every step each particle sits exactly one radius from its predecessor.</summary>
        [Fact]
        public void UpdateParticles_RadiusInvariant()
        {
            var strand = MakeStrand();

            for (int step = 1; step <= 3; step++)
            {
                CubismPhysics.UpdateParticles(
                    strand, 0, strand.Length,
                    0.1f, -0.05f, 30f, 0.2f, 0.1f, 0.001f, 1f / 30f, AirResistance);

                for (int i = 1; i < strand.Length; i++)
                {
                    float dx = strand[i].PositionX - strand[i - 1].PositionX;
                    float dy = strand[i].PositionY - strand[i - 1].PositionY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    Assert.True(
                        MathF.Abs(distance - strand[i].Radius) <= 0.001f,
                        $"particle {i} step {step}: distance {distance} != radius {strand[i].Radius}");
                }
            }
        }

        /// <summary>A zero delay skips the velocity update entirely (SDK branch on delay).</summary>
        [Fact]
        public void UpdateParticles_ZeroDelay_KeepsVelocity()
        {
            var strand = new[]
            {
                new CubismPhysicsParticle { PositionX = 0f, PositionY = 0f, LastGravityX = 0f, LastGravityY = -1f },
                new CubismPhysicsParticle
                {
                    PositionX = 0f, PositionY = -0.2f, Radius = 0.2f, Delay = 0f,
                    Mobility = 0.7f, Acceleration = 1f, LastGravityX = 0f, LastGravityY = -1f,
                    VelocityX = 0.123f, VelocityY = -0.456f,
                },
            };

            CubismPhysics.UpdateParticles(
                strand, 0, strand.Length, 0f, 0f, 0f, 0f, 0f, 0.001f, 1f / 30f, AirResistance);

            // Velocity stays untouched because delay == 0.
            Assert.Equal(0.123f, strand[1].VelocityX, FloatTolerance);
            Assert.Equal(-0.456f, strand[1].VelocityY, FloatTolerance);

            // Force accumulators are reset and the gravity direction is stored.
            Assert.Equal(0f, strand[1].ForceX, FloatTolerance);
            Assert.Equal(0f, strand[1].ForceY, FloatTolerance);
            Assert.Equal(0f, strand[1].LastGravityX, FloatTolerance);
            Assert.Equal(1f, strand[1].LastGravityY, FloatTolerance);
        }

        // ------------------------------------------------------------------
        // UpdateParticlesForStabilization (CubismPhysics.cpp lines 354-396)
        // ------------------------------------------------------------------

        /// <summary>Stabilization zeroes velocities and snaps to normalized-force radii.</summary>
        [Fact]
        public void UpdateParticlesForStabilization_MatchesTranscription()
        {
            var modStrand = MakeStrand();
            var referenceStrand = MakeStrand();

            for (int step = 1; step <= 3; step++)
            {
                CubismPhysics.UpdateParticlesForStabilization(
                    modStrand, 0, modStrand.Length,
                    0.1f, -0.05f, 30f, 0.2f, 0.1f, 0.001f);

                PhysicsTranscription.UpdateParticlesForStabilization(
                    referenceStrand, 0, referenceStrand.Length,
                    0.1f, -0.05f, 30f, 0.2f, 0.1f, 0.001f);

                AssertStrandsEqual(modStrand, referenceStrand, $"stabilization step {step}");

                for (int i = 1; i < modStrand.Length; i++)
                {
                    Assert.Equal(0f, modStrand[i].VelocityX, FloatTolerance);
                    Assert.Equal(0f, modStrand[i].VelocityY, FloatTolerance);
                }
            }
        }

        // ------------------------------------------------------------------
        // UpdateOutputParameterValue (CubismPhysics.cpp lines 398-432)
        // ------------------------------------------------------------------

        /// <summary>Values below the minimum clamp and are recorded for diagnostics.</summary>
        [Fact]
        public void UpdateOutputParameterValue_ClampsBelowMinimum()
        {
            var output = MakeAngleOutput(angleScale: 1f, weight: 100f);
            float parameterValue = 0f;

            CubismPhysics.UpdateOutputParameterValue(ref parameterValue, -0.5f, 0.5f, -10f, ref output);

            Assert.Equal(-0.5f, parameterValue, FloatTolerance);
            Assert.Equal(-10f, output.ValueBelowMinimum, FloatTolerance);
        }

        /// <summary>Values above the maximum clamp and are recorded for diagnostics.</summary>
        [Fact]
        public void UpdateOutputParameterValue_ClampsAboveMaximum()
        {
            var output = MakeAngleOutput(angleScale: 1f, weight: 100f);
            float parameterValue = 0f;

            CubismPhysics.UpdateOutputParameterValue(ref parameterValue, -0.5f, 0.5f, 10f, ref output);

            Assert.Equal(0.5f, parameterValue, FloatTolerance);
            Assert.Equal(10f, output.ValueExceededMaximum, FloatTolerance);
        }

        /// <summary>Output weight (0..100) blends the parameter toward the computed value.</summary>
        [Fact]
        public void UpdateOutputParameterValue_BlendsByWeight()
        {
            var output = MakeAngleOutput(angleScale: 1f, weight: 50f);
            float parameterValue = 0.6f;

            CubismPhysics.UpdateOutputParameterValue(ref parameterValue, -1f, 1f, 0.2f, ref output);

            float value = 0.2f * 1f; // translation * angleScale, inside [min, max]
            Assert.Equal(0.6f * 0.5f + value * 0.5f, parameterValue, FloatTolerance);
        }

        /// <summary>Weight 100 assigns the computed value directly (SDK weight >= 1 branch).</summary>
        [Fact]
        public void UpdateOutputParameterValue_FullWeight_AssignsExactValue()
        {
            var output = MakeAngleOutput(angleScale: 30f, weight: 100f);
            float parameterValue = 0.9f;

            CubismPhysics.UpdateOutputParameterValue(ref parameterValue, -360f, 360f, 1.5f, ref output);

            Assert.Equal(1.5f * 30f, parameterValue, FloatTolerance);
        }

        // ------------------------------------------------------------------
        // .physics3.json parsing against the Haru sample
        // ------------------------------------------------------------------

        /// <summary>All CubismPhysicsJson getters agree with an independent raw JSON scan.</summary>
        [Fact]
        public void PhysicsJson_HaruGetters_MatchRawJson()
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Live2DTestRuntime.SdkResources, "Haru", "Haru.physics3.json"));
            var json = new CubismPhysicsJson(bytes);

            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var meta = root.GetProperty("Meta");

            json.GetGravity(out float gravityX, out float gravityY);
            Assert.Equal(meta.GetProperty("EffectiveForces").GetProperty("Gravity").GetProperty("X").GetSingle(), gravityX, FloatTolerance);
            Assert.Equal(meta.GetProperty("EffectiveForces").GetProperty("Gravity").GetProperty("Y").GetSingle(), gravityY, FloatTolerance);

            json.GetWind(out float windX, out float windY);
            Assert.Equal(meta.GetProperty("EffectiveForces").GetProperty("Wind").GetProperty("X").GetSingle(), windX, FloatTolerance);
            Assert.Equal(meta.GetProperty("EffectiveForces").GetProperty("Wind").GetProperty("Y").GetSingle(), windY, FloatTolerance);

            // Meta.Fps is optional in .physics3.json; Haru omits it and the getter
            // reads 0 through the native error-value accessor behavior.
            Assert.Equal(
                meta.TryGetProperty("Fps", out var eFps) ? eFps.GetSingle() : 0f,
                json.GetFps(), FloatTolerance);
            Assert.Equal(meta.GetProperty("PhysicsSettingCount").GetInt32(), json.GetSubRigCount());
            Assert.Equal(meta.GetProperty("TotalInputCount").GetInt32(), json.GetTotalInputCount());
            Assert.Equal(meta.GetProperty("TotalOutputCount").GetInt32(), json.GetTotalOutputCount());
            Assert.Equal(meta.GetProperty("VertexCount").GetInt32(), json.GetVertexCount());

            var settings = root.GetProperty("PhysicsSettings");
            for (int si = 0; si < settings.GetArrayLength(); si++)
            {
                var setting = settings[si];

                json.GetNormalizationPosition(si, out var position);
                var rawPosition = setting.GetProperty("Normalization").GetProperty("Position");
                Assert.Equal(rawPosition.GetProperty("Minimum").GetSingle(), position.Minimum, FloatTolerance);
                Assert.Equal(rawPosition.GetProperty("Maximum").GetSingle(), position.Maximum, FloatTolerance);
                Assert.Equal(rawPosition.GetProperty("Default").GetSingle(), position.Default, FloatTolerance);

                json.GetNormalizationAngle(si, out var angle);
                var rawAngle = setting.GetProperty("Normalization").GetProperty("Angle");
                Assert.Equal(rawAngle.GetProperty("Minimum").GetSingle(), angle.Minimum, FloatTolerance);
                Assert.Equal(rawAngle.GetProperty("Maximum").GetSingle(), angle.Maximum, FloatTolerance);
                Assert.Equal(rawAngle.GetProperty("Default").GetSingle(), angle.Default, FloatTolerance);

                var inputs = setting.GetProperty("Input");
                Assert.Equal(inputs.GetArrayLength(), json.GetInputCount(si));
                for (int ii = 0; ii < inputs.GetArrayLength(); ii++)
                {
                    var rawInput = inputs[ii];
                    Assert.Equal(rawInput.GetProperty("Source").GetProperty("Id").GetString(),
                        json.GetInputSourceId(si, ii));
                    Assert.Equal(rawInput.GetProperty("Type").GetString(), json.GetInputType(si, ii));
                    Assert.Equal(rawInput.GetProperty("Weight").GetSingle(), json.GetInputWeight(si, ii), FloatTolerance);
                    Assert.Equal(rawInput.GetProperty("Reflect").GetBoolean(), json.GetInputReflect(si, ii));
                }

                var outputs = setting.GetProperty("Output");
                Assert.Equal(outputs.GetArrayLength(), json.GetOutputCount(si));
                for (int oi = 0; oi < outputs.GetArrayLength(); oi++)
                {
                    var rawOutput = outputs[oi];
                    Assert.Equal(rawOutput.GetProperty("Destination").GetProperty("Id").GetString(),
                        json.GetOutputsDestinationId(si, oi));
                    Assert.Equal(rawOutput.GetProperty("Type").GetString(), json.GetOutputType(si, oi));
                    Assert.Equal(rawOutput.GetProperty("Scale").GetSingle(),
                        json.GetOutputAngleScale(si, oi), FloatTolerance);
                    Assert.Equal(rawOutput.GetProperty("Weight").GetSingle(), json.GetOutputWeight(si, oi), FloatTolerance);
                    Assert.Equal(rawOutput.GetProperty("VertexIndex").GetInt32(), json.GetOutputVertexIndex(si, oi));
                }

                var vertices = setting.GetProperty("Vertices");
                Assert.Equal(vertices.GetArrayLength(), json.GetParticleCount(si));
                for (int vi = 0; vi < vertices.GetArrayLength(); vi++)
                {
                    var rawVertex = vertices[vi];
                    json.GetParticlePosition(si, vi, out float px, out float py);
                    Assert.Equal(rawVertex.GetProperty("Position").GetProperty("X").GetSingle(), px, FloatTolerance);
                    Assert.Equal(rawVertex.GetProperty("Position").GetProperty("Y").GetSingle(), py, FloatTolerance);
                    Assert.Equal(rawVertex.GetProperty("Radius").GetSingle(), json.GetParticleRadius(si, vi), FloatTolerance);
                    Assert.Equal(rawVertex.GetProperty("Delay").GetSingle(), json.GetParticleDelay(si, vi), FloatTolerance);
                    Assert.Equal(rawVertex.GetProperty("Mobility").GetSingle(), json.GetParticleMobility(si, vi), FloatTolerance);
                    Assert.Equal(rawVertex.GetProperty("Acceleration").GetSingle(),
                        json.GetParticleAcceleration(si, vi), FloatTolerance);
                }
            }
        }

        /// <summary>The full Haru physics rig parses without throwing (Parse + Initialize).</summary>
        [Fact]
        public void PhysicsCtor_HaruSample_Parses()
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Live2DTestRuntime.SdkResources, "Haru", "Haru.physics3.json"));
            var physics = new CubismPhysics(bytes);
            Assert.NotNull(physics);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Creates two-identical-ready four-particle strands with realistic values.</summary>
        private static CubismPhysicsParticle[] MakeStrand()
        {
            return new[]
            {
                new CubismPhysicsParticle
                {
                    InitialX = 0f, InitialY = 0f, PositionX = 0f, PositionY = 0f,
                    LastGravityX = 0f, LastGravityY = -1f,
                },
                new CubismPhysicsParticle
                {
                    InitialX = 0f, InitialY = -0.2f, PositionX = 0f, PositionY = -0.2f,
                    Radius = 0.2f, Delay = 0.5f, Mobility = 0.4f, Acceleration = 1f,
                    LastGravityX = 0f, LastGravityY = -1f,
                },
                new CubismPhysicsParticle
                {
                    InitialX = 0f, InitialY = -0.4f, PositionX = 0f, PositionY = -0.4f,
                    Radius = 0.2f, Delay = 1f, Mobility = 0.7f, Acceleration = 1f,
                    LastGravityX = 0f, LastGravityY = -1f,
                },
                new CubismPhysicsParticle
                {
                    InitialX = 0f, InitialY = -0.6f, PositionX = 0f, PositionY = -0.6f,
                    Radius = 0.2f, Delay = 1.5f, Mobility = 1f, Acceleration = 1f,
                    LastGravityX = 0f, LastGravityY = -1f,
                },
            };
        }

        private static CubismPhysicsOutput MakeAngleOutput(float angleScale, float weight)
        {
            return new CubismPhysicsOutput
            {
                Type = CubismPhysicsSource.Angle,
                AngleScale = angleScale,
                Weight = weight,
            };
        }

        private static void AssertStrandsEqual(CubismPhysicsParticle[] modStrand, CubismPhysicsParticle[] referenceStrand, string step)
        {
            for (int i = 0; i < modStrand.Length; i++)
            {
                Assert.True(MathF.Abs(modStrand[i].PositionX - referenceStrand[i].PositionX) <= FloatTolerance,
                    $"{step} particle {i} PositionX");
                Assert.True(MathF.Abs(modStrand[i].PositionY - referenceStrand[i].PositionY) <= FloatTolerance,
                    $"{step} particle {i} PositionY");
                Assert.True(MathF.Abs(modStrand[i].VelocityX - referenceStrand[i].VelocityX) <= FloatTolerance,
                    $"{step} particle {i} VelocityX");
                Assert.True(MathF.Abs(modStrand[i].VelocityY - referenceStrand[i].VelocityY) <= FloatTolerance,
                    $"{step} particle {i} VelocityY");
                Assert.True(MathF.Abs(modStrand[i].LastGravityX - referenceStrand[i].LastGravityX) <= FloatTolerance,
                    $"{step} particle {i} LastGravityX");
                Assert.True(MathF.Abs(modStrand[i].LastGravityY - referenceStrand[i].LastGravityY) <= FloatTolerance,
                    $"{step} particle {i} LastGravityY");
                Assert.True(MathF.Abs(modStrand[i].ForceX - referenceStrand[i].ForceX) <= FloatTolerance,
                    $"{step} particle {i} ForceX");
                Assert.True(MathF.Abs(modStrand[i].ForceY - referenceStrand[i].ForceY) <= FloatTolerance,
                    $"{step} particle {i} ForceY");
            }
        }
    }

    /// <summary>
    /// Independent transcription of the SDK 5-r.5 physics update math
    /// (CubismPhysics.cpp). Kept in a separate static class so the test methods
    /// stay free of support noise.
    /// </summary>
    internal static class PhysicsTranscription
    {
        private static float DegreesToRadian(float degrees) => (degrees / 180f) * MathF.PI;

        private static float DirectionToRadian(float fromX, float fromY, float toX, float toY)
        {
            float ret = MathF.Atan2(toY, toX) - MathF.Atan2(fromY, fromX);
            while (ret < -MathF.PI) ret += MathF.PI * 2f;
            while (ret > MathF.PI) ret -= MathF.PI * 2f;
            return ret;
        }

        private static void Normalize(ref float x, ref float y)
        {
            float length = MathF.Pow(x * x + y * y, 0.5f);
            x = x / length;
            y = y / length;
        }

        /// <summary>Transcription of CubismPhysics::UpdateParticles (lines 277-352).</summary>
        public static void UpdateParticles(
            CubismPhysicsParticle[] strand, int baseParticleIndex, int strandCount,
            float totalTranslationX, float totalTranslationY, float totalAngle,
            float windX, float windY, float thresholdValue, float deltaTimeSeconds, float airResistance)
        {
            strand[baseParticleIndex].PositionX = totalTranslationX;
            strand[baseParticleIndex].PositionY = totalTranslationY;

            float totalRadian = DegreesToRadian(totalAngle);
            float currentGravityX = MathF.Sin(totalRadian);
            float currentGravityY = MathF.Cos(totalRadian);
            Normalize(ref currentGravityX, ref currentGravityY);

            for (int i = 1; i < strandCount; ++i)
            {
                int idx = baseParticleIndex + i;

                strand[idx].ForceX = currentGravityX * strand[idx].Acceleration + windX;
                strand[idx].ForceY = currentGravityY * strand[idx].Acceleration + windY;

                strand[idx].LastPositionX = strand[idx].PositionX;
                strand[idx].LastPositionY = strand[idx].PositionY;

                float delay = strand[idx].Delay * deltaTimeSeconds * 30f;

                float directionX = strand[idx].PositionX - strand[idx - 1].PositionX;
                float directionY = strand[idx].PositionY - strand[idx - 1].PositionY;

                float radian = DirectionToRadian(
                    strand[idx].LastGravityX, strand[idx].LastGravityY,
                    currentGravityX, currentGravityY) / airResistance;

                // Native quirk: Y is rotated from the already-updated X.
                float newDirectionX = MathF.Cos(radian) * directionX - directionY * MathF.Sin(radian);
                directionY = MathF.Sin(radian) * newDirectionX + directionY * MathF.Cos(radian);
                directionX = newDirectionX;

                strand[idx].PositionX = strand[idx - 1].PositionX + directionX;
                strand[idx].PositionY = strand[idx - 1].PositionY + directionY;

                float velocityX = strand[idx].VelocityX * delay;
                float velocityY = strand[idx].VelocityY * delay;
                float forceX = strand[idx].ForceX * delay * delay;
                float forceY = strand[idx].ForceY * delay * delay;

                strand[idx].PositionX = strand[idx].PositionX + velocityX + forceX;
                strand[idx].PositionY = strand[idx].PositionY + velocityY + forceY;

                float deltaX = strand[idx].PositionX - strand[idx - 1].PositionX;
                float deltaY = strand[idx].PositionY - strand[idx - 1].PositionY;
                Normalize(ref deltaX, ref deltaY);

                strand[idx].PositionX = strand[idx - 1].PositionX + deltaX * strand[idx].Radius;
                strand[idx].PositionY = strand[idx - 1].PositionY + deltaY * strand[idx].Radius;

                if (MathF.Abs(strand[idx].PositionX) < thresholdValue)
                    strand[idx].PositionX = 0f;

                if (delay != 0f)
                {
                    strand[idx].VelocityX = strand[idx].PositionX - strand[idx].LastPositionX;
                    strand[idx].VelocityY = strand[idx].PositionY - strand[idx].LastPositionY;
                    strand[idx].VelocityX /= delay;
                    strand[idx].VelocityY /= delay;
                    strand[idx].VelocityX *= strand[idx].Mobility;
                    strand[idx].VelocityY *= strand[idx].Mobility;
                }

                strand[idx].ForceX = 0f;
                strand[idx].ForceY = 0f;
                strand[idx].LastGravityX = currentGravityX;
                strand[idx].LastGravityY = currentGravityY;
            }
        }

        /// <summary>Transcription of CubismPhysics::UpdateParticlesForStabilization (lines 354-396).</summary>
        public static void UpdateParticlesForStabilization(
            CubismPhysicsParticle[] strand, int baseParticleIndex, int strandCount,
            float totalTranslationX, float totalTranslationY, float totalAngle,
            float windX, float windY, float thresholdValue)
        {
            strand[baseParticleIndex].PositionX = totalTranslationX;
            strand[baseParticleIndex].PositionY = totalTranslationY;

            float totalRadian = DegreesToRadian(totalAngle);
            float currentGravityX = MathF.Sin(totalRadian);
            float currentGravityY = MathF.Cos(totalRadian);
            Normalize(ref currentGravityX, ref currentGravityY);

            for (int i = 1; i < strandCount; ++i)
            {
                int idx = baseParticleIndex + i;

                strand[idx].ForceX = currentGravityX * strand[idx].Acceleration + windX;
                strand[idx].ForceY = currentGravityY * strand[idx].Acceleration + windY;

                strand[idx].LastPositionX = strand[idx].PositionX;
                strand[idx].LastPositionY = strand[idx].PositionY;

                strand[idx].VelocityX = 0f;
                strand[idx].VelocityY = 0f;

                float forceX = strand[idx].ForceX;
                float forceY = strand[idx].ForceY;
                Normalize(ref forceX, ref forceY);

                forceX *= strand[idx].Radius;
                forceY *= strand[idx].Radius;
                strand[idx].PositionX = strand[idx - 1].PositionX + forceX;
                strand[idx].PositionY = strand[idx - 1].PositionY + forceY;

                if (MathF.Abs(strand[idx].PositionX) < thresholdValue)
                    strand[idx].PositionX = 0f;

                strand[idx].ForceX = 0f;
                strand[idx].ForceY = 0f;
                strand[idx].LastGravityX = currentGravityX;
                strand[idx].LastGravityY = currentGravityY;
            }
        }
    }
}
