using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for motion3.json parsing and curve evaluation against the Cubism
    /// SDK 5-r.5 native sources (Framework/src/Motion/CubismMotion.cpp and
    /// Framework/src/Math/CubismMath.cpp). Real sample motion files from the SDK
    /// are used as fixtures, and meta counts are re-derived from the raw JSON with
    /// an independent segment scanner instead of trusting the mod.
    /// </summary>
    public class CubismMotionParseTests
    {
        private const float FloatTolerance = 0.0001f;

        public static TheoryData<string> SampleMotionRelativePaths => new TheoryData<string>
        {
            Path.Combine("Haru", "motions", "haru_g_idle.motion3.json"),
            Path.Combine("Haru", "motions", "haru_g_m01.motion3.json"),
            Path.Combine("Haru", "motions", "haru_g_m04.motion3.json"),
            Path.Combine("Hiyori", "motions", "Hiyori_m01.motion3.json"),
            Path.Combine("Wanko", "motions", "idle_01.motion3.json"),
            Path.Combine("Mark", "motions", "mark_m01.motion3.json"),
        };

        // ------------------------------------------------------------------
        // Parsing: meta block versus independent raw-JSON scan
        // ------------------------------------------------------------------

        /// <summary>
        /// CubismMotionJson meta getters must agree with the raw motion3.json for
        /// duration, fps, loop, bezier restriction and the fade defaults.
        /// </summary>
        [Theory]
        [MemberData(nameof(SampleMotionRelativePaths))]
        public void CubismMotionJson_MetaGetters_MatchRawJson(string relativePath)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Live2DTestRuntime.SdkResources, relativePath));
            var json = CubismMotionJsonTestsHelper.ParseRawJson(bytes);
            var motionJson = new CubismMotionJson(bytes);

            Assert.Equal(json.Meta.Duration, motionJson.GetDuration(), FloatTolerance);
            Assert.Equal(json.Meta.Fps, motionJson.GetFps(), FloatTolerance);
            Assert.Equal(json.Meta.Loop, motionJson.IsLoop());
            Assert.Equal(json.Meta.AreBeziersRestricted, motionJson.AreBeziersRestricted());
            Assert.Equal(json.Meta.CurveCount, motionJson.GetCurveCount());
            Assert.Equal(json.Meta.TotalSegmentCount, motionJson.GetTotalSegmentCount());
            Assert.Equal(json.Meta.TotalPointCount, motionJson.GetTotalPointCount());
            Assert.Equal(json.Meta.UserDataCount, motionJson.GetEventCount());

            // FadeInTime is optional per motion; the SDK getter returns -1 when absent
            // (CubismMotionJson.cpp GetMotionFadeInTimeValue).
            if (json.Meta.FadeInTime == null)
                Assert.Equal(-1f, motionJson.GetFadeInTime(), FloatTolerance);
            else
                Assert.Equal(json.Meta.FadeInTime.Value, motionJson.GetFadeInTime(), FloatTolerance);

            if (json.Meta.FadeOutTime == null)
                Assert.Equal(-1f, motionJson.GetFadeOutTime(), FloatTolerance);
            else
                Assert.Equal(json.Meta.FadeOutTime.Value, motionJson.GetFadeOutTime(), FloatTolerance);
        }

        /// <summary>
        /// The mod-reported segment and point counts must equal an independent
        /// scan over every curve's Curves[].Segments array. Layout per the native
        /// parse loop (CubismMotion.cpp ~line 720): the first two floats are the
        /// start point (time, value) — there is no leading point index — and each
        /// segment is a type marker plus data: bezier (type 1) adds 3 points over
        /// 7 floats, linear/stepped/inverseStepped (0/2/3) add 1 point over 3.
        /// </summary>
        [Theory]
        [MemberData(nameof(SampleMotionRelativePaths))]
        public void CubismMotionJson_SegmentAndPointCounts_MatchIndependentScan(string relativePath)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Live2DTestRuntime.SdkResources, relativePath));
            var json = CubismMotionJsonTestsHelper.ParseRawJson(bytes);
            var motionJson = new CubismMotionJson(bytes);

            int totalSegments = 0;
            int totalPoints = 0;
            for (int c = 0; c < json.Meta.CurveCount; c++)
            {
                var segments = json.Curves[c].Segments;
                int pointCount = 1;
                int segmentCount = 0;
                int i = 2; // first two floats are the start point (time, value)
                while (i < segments.Length)
                {
                    int type = (int)segments[i];
                    if (type == 1)
                    {
                        pointCount += 3;
                        i += 7;
                    }
                    else if (type == 0 || type == 2 || type == 3)
                    {
                        pointCount += 1;
                        i += 3;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Unknown segment type {type} at index {i} of curve {c}");
                    }

                    segmentCount++;
                }

                Assert.Equal(segments.Length, i); // scanner must consume the full array
                totalSegments += segmentCount;
                totalPoints += pointCount;
            }

            Assert.Equal(totalSegments, motionJson.GetTotalSegmentCount());
            Assert.Equal(totalPoints, motionJson.GetTotalPointCount());
        }

        /// <summary>Looping motions report duration -1 for GetDuration but keep the loop duration.</summary>
        [Theory]
        [MemberData(nameof(SampleMotionRelativePaths))]
        public void CubismMotion_LoopDuration_MatchesRawMeta(string relativePath)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Live2DTestRuntime.SdkResources, relativePath));
            var json = CubismMotionJsonTestsHelper.ParseRawJson(bytes);
            var motion = new CubismMotion(bytes);

            if (json.Meta.Loop)
            {
                Assert.Equal(-1f, motion.GetDuration(), FloatTolerance);
                Assert.Equal(json.Meta.Duration, motion.GetLoopDuration(), FloatTolerance);
            }
            else
            {
                Assert.Equal(json.Meta.Duration, motion.GetDuration(), FloatTolerance);
            }
        }

        /// <summary>mark_m01 carries user data events that GetFiredEvent returns in order.</summary>
        [Fact]
        public void CubismMotion_GetFiredEvent_MarkSample()
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Live2DTestRuntime.SdkResources, "Mark", "motions", "mark_m01.motion3.json"));
            var motion = new CubismMotion(bytes);

            // Event window semantics of CubismMotion::GetFiredEvent: before < t <= after.
            var fired = motion.GetFiredEvent(-1f, 1.133f + 0.001f);
            Assert.Single(fired);
            Assert.Equal("timing1", fired[0]);

            fired = motion.GetFiredEvent(1.133f + 0.001f, 3.667f + 0.001f);
            Assert.Single(fired);
            Assert.Equal("timing2", fired[0]);

            // Nothing fires again inside the same window.
            fired = motion.GetFiredEvent(3.667f + 0.001f, 3.7f);
            Assert.Empty(fired);
        }

        // ------------------------------------------------------------------
        // Segment parsing and evaluation on synthetic curves
        // ------------------------------------------------------------------

        /// <summary>Linear evaluation is clamped at the segment start and exact at the end.</summary>
        [Fact]
        public void EvaluateCurveInternal_Linear()
        {
            // point0 (t=0,v=0) + linear segment to (t=1,v=10)
            float[] raw = { 0f, 0f, 0f, 1f, 10f };

            Assert.Equal(0f, MotionTestHelper.Evaluate(raw, 0f), FloatTolerance);
            Assert.Equal(5f, MotionTestHelper.Evaluate(raw, 0.5f), FloatTolerance);
            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 1f), FloatTolerance);

            // Times outside [0,1] clamp at the segment bounds.
            Assert.Equal(0f, MotionTestHelper.Evaluate(raw, -0.5f), FloatTolerance);
            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 1.5f), FloatTolerance);
        }

        /// <summary>
        /// Unrestricted bezier segments go through the Cardano closed-form solution
        /// (CubismMotion.cpp BezierEvaluateCardanoInterpretation). The mod result is
        /// compared against an independent transcription of the C++ Cardano solver
        /// (CubismMath.cpp CardanoAlgorithmForBezier + de Casteljau at the solved t).
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(0.13f)]
        [InlineData(0.5f)]
        [InlineData(0.77f)]
        [InlineData(1f)]
        public void EvaluateCurveInternal_BezierCardano_MatchesCppTranscription(float time)
        {
            // ParseSegmentsInternal layout: start point (0,0), bezier marker,
            // control points (0.2,0.8)/(0.8,0.8), end point (1,1).
            float[] raw = { 0f, 0f, 1f, 0.2f, 0.8f, 0.8f, 0.8f, 1f, 1f };

            float actual = MotionTestHelper.Evaluate(raw, time, false, 1f, false);
            float expected = MotionTestHelper.BezierCardanoEvaluate(raw, time);
            Assert.Equal(expected, actual, FloatTolerance);
        }

        /// <summary>
        /// When beziers are restricted the SDK falls back to binary search sampling
        /// (CubismMotion.cpp BezierEvaluateBinarySearch: 20 iterations, x error 0.01,
        /// interval halving of the control polygon). The mod result is compared to an
        /// exact transcription of that algorithm.
        /// </summary>
        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.5f)]
        [InlineData(0.9f)]
        public void EvaluateCurveInternal_BezierRestricted_MatchesBinarySearchTranscription(float time)
        {
            // Start point (0,0), bezier marker, control points (0.5,0)/(0.5,1),
            // end point (1,1) — monotone in x as the binary search requires.
            float[] raw = { 0f, 0f, 1f, 0.5f, 0f, 0.5f, 1f, 1f, 1f };

            float actual = MotionTestHelper.Evaluate(raw, time, false, 1f, true);
            float expected = MotionTestHelper.BezierBinarySearchEvaluate(raw, time);
            Assert.Equal(expected, actual, FloatTolerance);
        }

        /// <summary>Stepped segments hold the previous value until the next key time.</summary>
        [Fact]
        public void EvaluateCurveInternal_Stepped()
        {
            // (0,0) stepped to (1,10): value jumps exactly at t=1.
            float[] raw = { 0f, 0f, 2f, 1f, 10f };

            Assert.Equal(0f, MotionTestHelper.Evaluate(raw, 0.999f), FloatTolerance);
            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 1f), FloatTolerance);
        }

        /// <summary>Inverse stepped segments jump to the target value immediately.</summary>
        [Fact]
        public void EvaluateCurveInternal_InverseStepped()
        {
            float[] raw = { 0f, 0f, 3f, 1f, 10f };

            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 0.5f), FloatTolerance);
            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 1f), FloatTolerance);
        }

        /// <summary>Evaluation past the last segment returns the last point value.</summary>
        [Fact]
        public void EvaluateCurveInternal_BeyondLastSegment_ReturnsLastValue()
        {
            float[] raw = { 0f, 0f, 0f, 1f, 5f, 2f, 3f, 10f };

            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 3f), FloatTolerance);
            Assert.Equal(10f, MotionTestHelper.Evaluate(raw, 42f), FloatTolerance);
        }

        /// <summary>A three-segment curve evaluates each piece between its key times.</summary>
        [Fact]
        public void EvaluateCurveInternal_MultiSegment()
        {
            // linear to (1,5), stepped to (2,7), linear to (3,0)
            float[] raw = { 0f, 0f, 0f, 1f, 5f, 2f, 2f, 7f, 0f, 3f, 0f };

            Assert.Equal(2.5f, MotionTestHelper.Evaluate(raw, 0.5f), FloatTolerance);
            Assert.Equal(5f, MotionTestHelper.Evaluate(raw, 1f), FloatTolerance);
            Assert.Equal(5f, MotionTestHelper.Evaluate(raw, 1.9f), FloatTolerance);
            Assert.Equal(7f, MotionTestHelper.Evaluate(raw, 2f), FloatTolerance);
            Assert.Equal(3.5f, MotionTestHelper.Evaluate(raw, 2.5f), FloatTolerance);
            Assert.Equal(0f, MotionTestHelper.Evaluate(raw, 3f), FloatTolerance);
        }

        /// <summary>
        /// Loop correction (CubismMotion.cpp CorrectEndPoint, V2 behavior when the
        /// motion loops with isCorrection enabled): the segment is re-evaluated
        /// against the loop end time endTime = duration + deltaTimeSeconds.
        /// </summary>
        [Fact]
        public void CorrectEndPointInternal_LoopCorrection()
        {
            float duration = 1f;
            float frame = 1f / 60f;
            float endTime = duration + 1f / 30f;

            // Linear loop correction at the frame after the end interpolates from the
            // last value (10) toward the looped first value (0) placed at endTime.
            float[] linear = { 0f, 0f, 0f, 1f, 10f };
            float corrected = MotionTestHelper.Evaluate(
                linear, duration + frame, true, endTime, false);
            Assert.Equal(5f, corrected, FloatTolerance);

            // A stepped final segment keeps its value under correction.
            float[] stepped = { 0f, 0f, 2f, 1f, 10f };
            Assert.Equal(10f, MotionTestHelper.Evaluate(
                stepped, duration + frame, true, endTime, false), FloatTolerance);

            // An inverse stepped final segment has already jumped to the next value.
            float[] inverse = { 0f, 0f, 3f, 1f, 10f };
            Assert.Equal(0f, MotionTestHelper.Evaluate(
                inverse, duration + frame, true, endTime, false), FloatTolerance);

            // At or after the loop end time the last value is returned.
            Assert.Equal(10f, MotionTestHelper.Evaluate(
                linear, endTime, true, endTime, false), FloatTolerance);
        }

        /// <summary>Expression motions default to a 1 second fade on both sides.</summary>
        [Fact]
        public void CubismExpressionMotion_DefaultFadeTime()
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Live2DTestRuntime.SdkResources, "Haru", "expressions", "F01.exp3.json"));
            var expression = new CubismExpressionMotion(bytes);

            Assert.Equal(1f, expression.FadeInSeconds, FloatTolerance);
            Assert.Equal(1f, expression.FadeOutSeconds, FloatTolerance);
        }
    }

    /// <summary>
    /// Raw motion3.json access and independent transcriptions of the C++ math used
    /// by EvaluateCurveInternal (CubismMath.cpp + CubismMotion.cpp, SDK 5-r.5).
    /// </summary>
    internal static class MotionTestHelper
    {
        private const float MathEpsilon = 0.00001f;

        public static float Evaluate(
            float[] rawSegments, float time, bool isCorrection = false, float endTime = 1f,
            bool restricted = false)
        {
            var points = new List<CubismMotionPoint>();
            var segments = new List<MotionSegmentData>();
            var curve = new MotionCurveData();
            curve.SegmentCount = CubismMotion.ParseSegmentsInternal(rawSegments, points, segments);

            return CubismMotion.EvaluateCurveInternal(
                curve, points.ToArray(), segments.ToArray(), time, isCorrection, endTime, restricted);
        }

        private static float RangeF(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        /// <summary>Transcription of CubismMath.cpp QuadraticEquation (lines 104-120).</summary>
        private static float QuadraticEquation(float a, float b, float c)
        {
            if (MathF.Abs(a) < MathEpsilon)
                return MathF.Abs(b) < MathEpsilon ? -c : -c / b;

            return -(b + MathF.Sqrt(b * b - 4f * a * c)) / (2f * a);
        }

        /// <summary>Transcription of CubismMath.cpp CardanoAlgorithmForBezier (lines 128-191).</summary>
        private static float CardanoAlgorithmForBezier(float a, float b, float c, float d)
        {
            if (MathF.Abs(a) < MathEpsilon)
                return RangeF(QuadraticEquation(b, c, d), 0f, 1f);

            float ba = b / a;
            float ca = c / a;
            float da = d / a;

            float p = (3f * ca - ba * ba) / 3f;
            float p3 = p / 3f;
            float q = (2f * ba * ba * ba - 9f * ba * ca + 27f * da) / 27f;
            float q2 = q / 2f;

            float discriminant = q2 * q2 + p3 * p3 * p3;
            const float center = 0.5f;
            const float threshold = center + 0.01f;

            if (discriminant < 0f)
            {
                float mp3 = -p / 3f;
                float mp33 = mp3 * mp3 * mp3;
                float r = MathF.Sqrt(mp33);
                float t = -q / (2f * r);
                float cosphi = RangeF(t, -1f, 1f);
                float phi = MathF.Acos(cosphi);
                float crtr = MathF.Cbrt(r);
                float t1 = 2f * crtr;

                float root1 = t1 * MathF.Cos(phi / 3f) - ba / 3f;
                if (MathF.Abs(root1 - center) < threshold)
                    return RangeF(root1, 0f, 1f);

                float root2 = t1 * MathF.Cos((phi + 2f * MathF.PI) / 3f) - ba / 3f;
                if (MathF.Abs(root2 - center) < threshold)
                    return RangeF(root2, 0f, 1f);

                float root3 = t1 * MathF.Cos((phi + 4f * MathF.PI) / 3f) - ba / 3f;
                return RangeF(root3, 0f, 1f);
            }

            if (discriminant == 0f)
            {
                float u1 = q2 < 0f ? MathF.Cbrt(-q2) : -MathF.Cbrt(q2);
                float root1 = 2f * u1 - ba / 3f;
                if (MathF.Abs(root1 - center) < threshold)
                    return RangeF(root1, 0f, 1f);

                float root2 = -u1 - ba / 3f;
                return RangeF(root2, 0f, 1f);
            }

            float sd = MathF.Sqrt(discriminant);
            float uc = MathF.Cbrt(sd - q2);
            float vc = MathF.Cbrt(sd + q2);
            return RangeF(uc - vc - ba / 3f, 0f, 1f);
        }

        /// <summary>
        /// Independent bezier evaluation: solve t with the C++ Cardano solver, then
        /// de Casteljau (mirrors BezierEvaluateCardanoInterpretation).
        /// raw = [x1,v1, 1, cx1,cy1, cx2,cy2, x2,v2] (ParseSegmentsInternal layout).
        /// </summary>
        public static float BezierCardanoEvaluate(float[] raw, float time)
        {
            float x1 = raw[0];
            float cx1 = raw[3];
            float cx2 = raw[5];
            float x2 = raw[7];

            float a = x2 - 3f * cx2 + 3f * cx1 - x1;
            float b = 3f * cx2 - 6f * cx1 + 3f * x1;
            float c = 3f * cx1 - 3f * x1;
            float d = x1 - time;
            float t = CardanoAlgorithmForBezier(a, b, c, d);

            return BezierDeCasteljau(raw, t);
        }

        /// <summary>
        /// Transcription of CubismMotion.cpp BezierEvaluateBinarySearch
        /// (20 iterations, x error tolerance 0.01, interval halving of the
        /// time control polygon, then de Casteljau over the original points).
        /// </summary>
        public static float BezierBinarySearchEvaluate(float[] raw, float time)
        {
            const float xError = 0.01f;

            float x = time;
            float x1 = raw[0];
            float x2 = raw[7];
            float cx1 = raw[3];
            float cx2 = raw[5];

            float ta = 0f;
            float tb = 1f;
            float t = 0f;
            int i = 0;

            for (; i < 20; i++)
            {
                if (x < x1 + xError)
                {
                    t = ta;
                    break;
                }

                if (x2 - xError < x)
                {
                    t = tb;
                    break;
                }

                float centerx = (cx1 + cx2) * 0.5f;
                cx1 = (x1 + cx1) * 0.5f;
                cx2 = (x2 + cx2) * 0.5f;
                float ctrlx12 = (cx1 + centerx) * 0.5f;
                float ctrlx21 = (cx2 + centerx) * 0.5f;
                centerx = (ctrlx12 + ctrlx21) * 0.5f;

                if (x < centerx)
                {
                    tb = (ta + tb) * 0.5f;
                    if (centerx - xError < x)
                    {
                        t = tb;
                        break;
                    }

                    x2 = centerx;
                    cx2 = ctrlx12;
                }
                else
                {
                    ta = (ta + tb) * 0.5f;
                    if (x < centerx + xError)
                    {
                        t = ta;
                        break;
                    }

                    x1 = centerx;
                    cx1 = ctrlx21;
                }
            }

            if (i == 20)
                t = (ta + tb) * 0.5f;

            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return BezierDeCasteljau(raw, t);
        }

        /// <summary>De Casteljau over the 9-float ParseSegmentsInternal bezier layout.</summary>
        private static float BezierDeCasteljau(float[] raw, float t)
        {
            var p0 = new CubismMotionPoint { Time = raw[0], Value = raw[1] };
            var p1 = new CubismMotionPoint { Time = raw[3], Value = raw[4] };
            var p2 = new CubismMotionPoint { Time = raw[5], Value = raw[6] };
            var p3 = new CubismMotionPoint { Time = raw[7], Value = raw[8] };

            var p01 = Lerp(p0, p1, t);
            var p12 = Lerp(p1, p2, t);
            var p23 = Lerp(p2, p3, t);
            var p012 = Lerp(p01, p12, t);
            var p123 = Lerp(p12, p23, t);
            return Lerp(p012, p123, t).Value;
        }

        private static CubismMotionPoint Lerp(CubismMotionPoint a, CubismMotionPoint b, float t)
        {
            return new CubismMotionPoint
            {
                Time = a.Time + (b.Time - a.Time) * t,
                Value = a.Value + (b.Value - a.Value) * t,
            };
        }
    }

    /// <summary>Raw motion3.json reader for building independent expectations.</summary>
    internal static class CubismMotionJsonTestsHelper
    {
        public sealed class RawCurve
        {
            public string Target { get; set; }
            public string Id { get; set; }
            public float[] Segments { get; set; }
        }

        public sealed class RawMotion
        {
            public int Version { get; set; }
            public RawMeta Meta { get; set; }
            public RawCurve[] Curves { get; set; }
        }

        public sealed class RawMeta
        {
            public int Version { get; set; }
            public float Duration { get; set; }
            public float Fps { get; set; }
            public bool Loop { get; set; }
            public bool AreBeziersRestricted { get; set; }
            public int CurveCount { get; set; }
            public int TotalSegmentCount { get; set; }
            public int TotalPointCount { get; set; }
            public int UserDataCount { get; set; }
            public int TotalUserDataSize { get; set; }
            public float? FadeInTime { get; set; }
            public float? FadeOutTime { get; set; }
        }

        public static RawMotion ParseRawJson(byte[] bytes)
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;

            var result = new RawMotion { Meta = new RawMeta(), Curves = Array.Empty<RawCurve>() };

            if (root.TryGetProperty("Meta", out var metaElement))
            {
                var meta = result.Meta;
                if (metaElement.TryGetProperty("Duration", out var eDuration)) meta.Duration = eDuration.GetSingle();
                if (metaElement.TryGetProperty("Fps", out var eFps)) meta.Fps = eFps.GetSingle();
                if (metaElement.TryGetProperty("Loop", out var eLoop)) meta.Loop = eLoop.GetBoolean();
                if (metaElement.TryGetProperty("AreBeziersRestricted", out var eRestricted)) meta.AreBeziersRestricted = eRestricted.GetBoolean();
                if (metaElement.TryGetProperty("CurveCount", out var eCurveCount)) meta.CurveCount = eCurveCount.GetInt32();
                if (metaElement.TryGetProperty("TotalSegmentCount", out var eSegCount)) meta.TotalSegmentCount = eSegCount.GetInt32();
                if (metaElement.TryGetProperty("TotalPointCount", out var ePointCount)) meta.TotalPointCount = ePointCount.GetInt32();
                if (metaElement.TryGetProperty("UserDataCount", out var eUserDataCount)) meta.UserDataCount = eUserDataCount.GetInt32();
                if (metaElement.TryGetProperty("TotalUserDataSize", out var eUserDataSize)) meta.TotalUserDataSize = eUserDataSize.GetInt32();
                if (metaElement.TryGetProperty("FadeInTime", out var eFadeIn) && eFadeIn.ValueKind == JsonValueKind.Number)
                    meta.FadeInTime = eFadeIn.GetSingle();
                if (metaElement.TryGetProperty("FadeOutTime", out var eFadeOut) && eFadeOut.ValueKind == JsonValueKind.Number)
                    meta.FadeOutTime = eFadeOut.GetSingle();
            }

            if (root.TryGetProperty("Curves", out var curvesElement)
                && curvesElement.ValueKind == JsonValueKind.Array)
            {
                var curves = new List<RawCurve>();
                foreach (var curveElement in curvesElement.EnumerateArray())
                {
                    var curve = new RawCurve();
                    if (curveElement.TryGetProperty("Target", out var eTarget)) curve.Target = eTarget.GetString();
                    if (curveElement.TryGetProperty("Id", out var eId)) curve.Id = eId.GetString();
                    if (curveElement.TryGetProperty("Segments", out var eSegments))
                    {
                        var floats = new List<float>();
                        foreach (var v in eSegments.EnumerateArray())
                            floats.Add(v.GetSingle());
                        curve.Segments = floats.ToArray();
                    }

                    curves.Add(curve);
                }

                result.Curves = curves.ToArray();
            }

            return result;
        }
    }
}
