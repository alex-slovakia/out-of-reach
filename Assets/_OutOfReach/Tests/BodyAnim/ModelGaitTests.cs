using NUnit.Framework;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Tests.BodyAnim
{
    /// <summary>Pure gait-model checks: planted feet, cadence for the real walk speed, continuous trajectories.</summary>
    public sealed class ModelGaitTests
    {
        private const float Leg = .90f;
        private static readonly float[] Speeds = { .8f, 1.5f, 2.5f, 3.75f };

        [Test]
        public void StanceFootDoesNotSlideInWorld()
        {
            foreach (float v in Speeds)
            {
                float stride = ModelGait.StrideLength(v, Leg), stance = ModelGait.Stance(v);
                const int n = 2000;
                float anchor = float.NaN, worst = 0f;
                for (int i = 0; i < n; i++)
                {
                    float phase = i / (float)n;
                    if (phase >= stance) continue;
                    float t = phase * stride / v;
                    float world = v * t + ModelGait.FootForeAft(phase, stride, stance);
                    if (float.IsNaN(anchor)) anchor = world;
                    worst = Mathf.Max(worst, Mathf.Abs(world - anchor));
                }
                Assert.Less(worst, .001f, "stance foot slid " + worst * 1000f + " mm at " + v + " m/s");
            }
        }

        [Test]
        public void FootTrajectoryIsContinuous()
        {
            foreach (float v in Speeds)
            {
                float stride = ModelGait.StrideLength(v, Leg), stance = ModelGait.Stance(v);
                const int n = 4000;
                float previous = ModelGait.FootForeAft(0f, stride, stance);
                float widest = 0f;
                for (int i = 1; i <= n; i++)
                {
                    float current = ModelGait.FootForeAft(i / (float)n, stride, stance);
                    widest = Mathf.Max(widest, Mathf.Abs(current - previous));
                    previous = current;
                }
                Assert.Less(widest, stride * 4f / n, "foot teleports at " + v + " m/s");
                Assert.AreEqual(ModelGait.FootForeAft(0f, stride, stance), ModelGait.FootForeAft(.99999f, stride, stance), 1e-3f);
            }
        }

        [Test]
        public void CadenceAtWalkSpeedIsNaturalAndSlowerThanTheOldMarch()
        {
            float walk = ModelGait.StepsPerMinute(ModelGait.WalkSpeed, Leg);
            float sprint = ModelGait.StepsPerMinute(ModelGait.WalkSpeed * ModelGait.SprintFactor, Leg);
            float carry = ModelGait.StepsPerMinute(ModelGait.WalkSpeed * .6f, Leg);
            Assert.That(walk, Is.InRange(135f, 175f), "walk " + walk + " steps/min (old model ~208)");
            Assert.That(sprint, Is.InRange(150f, 200f), "sprint " + sprint + " steps/min");
            Assert.Less(carry, walk, "slower travel must slow the cadence");
            Assert.Greater(ModelGait.StrideLength(2.5f, Leg), 1.5f);
        }

        [Test]
        public void WalkKeepsAFootDownAndSprintMayFly()
        {
            Assert.GreaterOrEqual(ModelGait.Stance(1.5f), .55f);
            Assert.GreaterOrEqual(ModelGait.Stance(2.5f), .47f);
            Assert.Less(ModelGait.Stance(3.75f), .45f);
        }

        [Test]
        public void SoleIsOnTheGroundThroughStanceAndLiftsOnceInSwing()
        {
            float stance = ModelGait.Stance(2.5f), lift = ModelGait.Lift(2.5f, Leg);
            float peak = 0f; int risingEdges = 0; float last = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float phase = i / 1000f, h = ModelGait.FootHeight(phase, stance, lift);
                if (phase < stance) Assert.AreEqual(0f, h, 1e-6f);
                else if (h > last && last <= 1e-6f) risingEdges++;
                peak = Mathf.Max(peak, h); last = h;
            }
            Assert.AreEqual(1, risingEdges);
            Assert.AreEqual(lift, peak, lift * .03f);
            Assert.That(lift, Is.InRange(.05f, .2f), "walk clearance should read as a step");
        }

        [Test]
        public void RockerKeepsTheContactPointPlanted()
        {
            const float heel = .065f, ball = .14f, h = .085f;
            for (float pitch = -32f; pitch <= 12f; pitch += 1f)
            {
                Vector2 shift = ModelGait.RockerAnkleShift(pitch, heel, ball, h);
                float t = pitch * Mathf.Deg2Rad, c = Mathf.Cos(t), s = Mathf.Sin(t);
                Vector2 pivot = pitch >= 0f ? new Vector2(-heel, -h) : new Vector2(ball, -h);
                Vector2 world = shift + new Vector2(pivot.x * c - pivot.y * s, pivot.x * s + pivot.y * c);
                Assert.AreEqual(pivot.x, world.x, 1e-5f, "contact point moved at pitch " + pitch);
                Assert.AreEqual(pivot.y, world.y, 1e-5f);
            }
            Assert.AreEqual(Vector2.zero, ModelGait.RockerAnkleShift(0f, heel, ball, h));
        }

        [Test]
        public void FootPitchIsContinuousAcrossTheCycleAndRollsHeelToToe()
        {
            foreach (float v in Speeds)
            {
                float stance = ModelGait.Stance(v), previous = ModelGait.FootPitch(0f, stance, v);
                Assert.Greater(previous, 3f, "heel strike should land toes-up");
                float min = 0f;
                for (int i = 1; i <= 2000; i++)
                {
                    float p = ModelGait.FootPitch(i / 2000f, stance, v);
                    Assert.Less(Mathf.Abs(p - previous), 2f, "pitch jump at " + v + " m/s");
                    previous = p; min = Mathf.Min(min, p);
                }
                Assert.Less(min, -15f, "heel must rise before toe-off");
                Assert.AreEqual(ModelGait.FootPitch(0f, stance, v), ModelGait.FootPitch(.99999f, stance, v), .5f);
            }
        }

        [Test]
        public void LookDownSetbackIsMonotonicBoundedAndZeroLevel()
        {
            Assert.AreEqual(0f, ModelGait.LookDownSetback(0f, Leg), 1e-6f);
            Assert.AreEqual(0f, ModelGait.LookDownSetback(-30f, Leg), 1e-6f, "looking up must not move the pelvis");
            float last = 0f;
            for (float a = 0f; a <= 90f; a += 1f)
            {
                float s = ModelGait.LookDownSetback(a, Leg);
                Assert.GreaterOrEqual(s, last - 1e-6f); last = s;
            }
            Assert.LessOrEqual(last, Leg * .13f + 1e-6f);
        }

        [Test]
        public void PelvisSwaysTowardsTheStanceFoot()
        {
            Assert.Less(ModelGait.PelvisSway(.25f, Leg, 2.5f), -.005f);
            Assert.Greater(ModelGait.PelvisSway(.75f, Leg, 2.5f), .005f);
            Assert.Less(Mathf.Abs(ModelGait.PelvisSway(.25f, Leg, 2.5f)), .03f);
        }
    }
}
