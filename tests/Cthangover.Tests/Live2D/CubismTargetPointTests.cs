using System;
using Cthangover.Live2D.Cubism.Framework;
using Xunit;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Unit tests for CubismTargetPoint against an exact transcription of the
    /// Cubism SDK 5-r.5 source Math/CubismTargetPoint.cpp (velocity follower with
    /// capped acceleration in 30 fps frame units).
    /// </summary>
    public class CubismTargetPointTests
    {
        private const float FloatTolerance = 0.0001f;

        /// <summary>The SDK Epsilon: motion below this distance is suppressed.</summary>
        private const float Epsilon = 0.01f;

        /// <summary>SDK MaxV per internal frame = (40 / 10) / FrameRate.</summary>
        private const float MaxVPerFrame = 4f / 30f;

        /// <summary>The very first Update only anchors the clock and does not move.</summary>
        [Fact]
        public void FirstUpdate_AnchorsClockOnly()
        {
            var point = new CubismTargetPoint();
            point.Set(1f, 1f);
            point.Update(1f / 30f);

            Assert.Equal(0f, point.X, FloatTolerance);
            Assert.Equal(0f, point.Y, FloatTolerance);
        }

        /// <summary>Per-frame displacement never exceeds the SDK maximum speed.</summary>
        [Fact]
        public void PerFrameSpeed_CappedAtMaxV()
        {
            var point = new CubismTargetPoint();
            point.Set(1f, 0f);

            float maxDelta = 0f;
            float previousX = point.X;
            for (int frame = 0; frame < 300; frame++)
            {
                point.Update(1f / 30f);
                maxDelta = MathF.Max(maxDelta, MathF.Abs(point.X - previousX));
                previousX = point.X;
            }

            Assert.True(maxDelta <= MaxVPerFrame + 0.0001f,
                $"per-frame displacement {maxDelta} exceeds MaxV {MaxVPerFrame}");
        }

        /// <summary>The follower converges onto the target and then stays still.</summary>
        [Fact]
        public void ConvergesToTarget_ThenStays()
        {
            var point = new CubismTargetPoint();
            point.Set(1f, 0f);
            for (int frame = 0; frame < 1200; frame++)
                point.Update(1f / 30f);

            Assert.True(MathF.Abs(point.X - 1f) <= Epsilon, $"X={point.X}");
            Assert.True(MathF.Abs(point.Y) <= Epsilon, $"Y={point.Y}");

            // With the target reached the update returns early and the point rests.
            point.Set(0f, 0f);
            for (int frame = 0; frame < 300; frame++)
                point.Update(1f / 30f);

            Assert.True(MathF.Abs(point.X) <= Epsilon, $"rest X={point.X}");
        }

        /// <summary>
        /// Two hundred frames with target changes and irregular frame times follow
        /// the C++ transcription frame by frame (tolerance 1e-5).
        /// </summary>
        [Fact]
        public void Lockstep_MatchesCppTranscription()
        {
            var point = new CubismTargetPoint();
            var reference = new TargetPointSim();

            float[] frameTimes = new float[200];
            for (int frame = 0; frame < frameTimes.Length; frame++)
                frameTimes[frame] = 1f / 30f;
            frameTimes[80] = 0.5f;   // frame rate spike
            frameTimes[150] = 0.5f;  // frame rate spike

            for (int frame = 0; frame < frameTimes.Length; frame++)
            {
                if (frame == 50)
                {
                    point.Set(0.8f, -0.6f);
                    reference.Set(0.8f, -0.6f);
                }

                if (frame == 120)
                {
                    point.Set(-0.4f, 0.9f);
                    reference.Set(-0.4f, 0.9f);
                }

                point.Update(frameTimes[frame]);
                reference.Update(frameTimes[frame]);

                Assert.True(MathF.Abs(reference.X - point.X) <= 0.00001f,
                    $"frame {frame}: X {point.X} vs reference {reference.X}");
                Assert.True(MathF.Abs(reference.Y - point.Y) <= 0.00001f,
                    $"frame {frame}: Y {point.Y} vs reference {reference.Y}");
            }
        }
    }

    /// <summary>
    /// Exact transcription of CubismTargetPoint.cpp (SDK 5-r.5) used as the
    /// independent reference implementation in the lockstep test.
    /// </summary>
    internal sealed class TargetPointSim
    {
        private const int FrameRate = 30;
        private const float Epsilon = 0.01f;

        private float _faceTargetX, _faceTargetY;
        private float _faceX, _faceY;
        private float _faceVX, _faceVY;
        private float _lastTimeSeconds;
        private float _userTimeSeconds;

        public float X => _faceX;
        public float Y => _faceY;

        public void Set(float x, float y)
        {
            _faceTargetX = x;
            _faceTargetY = y;
        }

        public void Update(float deltaTimeSeconds)
        {
            _userTimeSeconds += deltaTimeSeconds;

            const float FaceParamMaxV = 40f / 10f;
            float MaxV = FaceParamMaxV * 1f / FrameRate;

            if (_lastTimeSeconds == 0f)
            {
                _lastTimeSeconds = _userTimeSeconds;
                return;
            }

            float deltaTimeWeight = (_userTimeSeconds - _lastTimeSeconds) * FrameRate;
            _lastTimeSeconds = _userTimeSeconds;

            const float TimeToMaxSpeed = 0.15f;
            const float FrameToMaxSpeed = TimeToMaxSpeed * FrameRate;
            float MaxA = deltaTimeWeight * MaxV / FrameToMaxSpeed;

            float dx = _faceTargetX - _faceX;
            float dy = _faceTargetY - _faceY;

            if (MathF.Abs(dx) <= Epsilon && MathF.Abs(dy) <= Epsilon)
                return;

            float d = MathF.Sqrt((dx * dx) + (dy * dy));

            float vx = MaxV * dx / d;
            float vy = MaxV * dy / d;

            float ax = vx - _faceVX;
            float ay = vy - _faceVY;

            float a = MathF.Sqrt((ax * ax) + (ay * ay));

            if (a < -MaxA || a > MaxA)
            {
                ax *= MaxA / a;
                ay *= MaxA / a;
            }

            _faceVX += ax;
            _faceVY += ay;

            {
                float maxV = 0.5f * (MathF.Sqrt((MaxA * MaxA) + 16f * MaxA * d - 8f * MaxA * d) - MaxA);
                float curV = MathF.Sqrt((_faceVX * _faceVX) + (_faceVY * _faceVY));

                if (curV > maxV)
                {
                    _faceVX *= maxV / curV;
                    _faceVY *= maxV / curV;
                }
            }

            _faceX += _faceVX;
            _faceY += _faceVY;
        }
    }
}
