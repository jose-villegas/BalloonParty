using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     What an idle cap does (<c>Plans/PLAN-MeadowMushrooms.md</c>, <i>An idle cap</i>) is the shader's work,
    ///     from the vectors <see cref="MushroomIdleTuning" /> hands it. These hold the vectors' arithmetic, and the
    ///     shipped numbers against the three things they must not break: a bounce still looks like it touches the
    ///     cap, the field's rocking stays well under a hit's lean, and the quad the mushroom is drawn on has the
    ///     room <see cref="MushroomQuadRoom" /> says the numbers need.
    /// </summary>
    [TestFixture]
    public class MushroomIdleTuningTests
    {
        private const string QuadPath = "Assets/Prefabs/Grid/Mushroom/MushroomQuad.asset";
        private const string MaterialPath = "Assets/Materials/Grid/Mushroom.mat";
        private const float Turn = 2f * Mathf.PI;
        private const float Radius = 0.3f;
        private const float PulseSwell = 0.22f;

        private static readonly MushroomSettings Shipped = new();

        [Test]
        public void TheBreathsVector_IsTheSwell_ThePaceInRadiansASecond_AndThePhasesInRadians()
        {
            var tuning = Tuning(breath: 0.06f, breathSeconds: 2f, phaseSpread: 0.5f, waveAcross: 0.25f);

            var vector = tuning.BreathVector;

            Assert.AreEqual(0.06f, vector.x, 1e-6f);
            Assert.AreEqual(Turn / 2f, vector.y, 1e-5f, "a breath every two seconds");
            Assert.AreEqual(Turn * 0.5f, vector.z, 1e-5f, "seeds half a breath apart at the most");
            Assert.AreEqual(Turn * 0.25f, vector.w, 1e-5f, "a quarter of a breath a world unit");
        }

        [Test]
        public void ANegativeBreath_IsNone_SoTheCapIsNeverDrawnUnderItsCollider()
        {
            var tuning = Tuning(breath: -0.2f, breathSeconds: 0f, phaseSpread: 3f, waveAcross: -1f);

            var vector = tuning.BreathVector;

            Assert.AreEqual(0f, vector.x);
            Assert.That(vector.y, Is.GreaterThan(0f).And.LessThan(100f), "a breath of no seconds still has a pace");
            Assert.AreEqual(Turn, vector.z, 1e-5f, "a spread over 1 is a whole breath");
            Assert.AreEqual(0f, vector.w);
        }

        [Test]
        public void TheRocksVector_IsInRadii_WithThePaceAndTheShareThatSwings()
        {
            var tuning = new MushroomIdleTuning(
                breath: 0f, breathSeconds: 2f, phaseSpread: 0f, waveAcross: 0f, shadowLift: 0f, glowSwing: 0f,
                wartSpread: 0f, rockPerDisplacement: 0.12f, rockMost: 0.06f, rockSeconds: 0.5f, rockSwing: 0.7f);

            var vector = tuning.RockVector(0.3f);

            Assert.AreEqual(0.4f, vector.x, 1e-5f);
            Assert.AreEqual(0.2f, vector.y, 1e-5f);
            Assert.AreEqual(Turn / 0.5f, vector.z, 1e-4f);
            Assert.AreEqual(0.7f, vector.w, 1e-6f);
            Assert.AreEqual(Vector4.zero, tuning.RockVector(0f), "a cap with no size does not rock");
        }

        [Test]
        public void TheShippedBreath_PutsTheCapNoFurtherPastItsColliderThanABounceAllows()
        {
            Assert.LessOrEqual(Shipped.Idle.Breath * Shipped.Radius, 0.03f,
                "the cap is drawn this much past the circle the ball bounces off at the most: about six pixels");
        }

        [Test]
        public void TheShippedRocking_StaysWellUnderAHitsLean_AndTheFieldAtRestMovesNothing()
        {
            var idle = Shipped.Idle;
            var hit = Shipped.Lean.LeanPerSpeed * Shipped.Spores.FullSpeed;

            Assert.LessOrEqual(idle.RockMost, 0.5f * hit, "a shot off the cannon leans the cap twice as far at the least");
            Assert.Less(idle.RockMost, Shipped.Lean.SwayLean, "and a blast's sway further");

            // What the field holds when nothing has passed, about 0.05 of displacement.
            var atRest = idle.RockMost * (float)System.Math.Tanh(0.05f * 0.05f * idle.RockPerDisplacement / idle.RockMost);
            Assert.Less(atRest, 0.001f, "under a quarter of a pixel");
        }

        [Test]
        public void TheQuadsRoom_GrowsWithTheBreath_TheRocking_AndTheBreathsShadow()
        {
            // No cut's room and no blown cap's lift, so the whole cap's own needs are what is measured.
            var lean = Shipped.Lean;
            var room = Room(Idle(breath: 0.08f, shadowLift: 0.8f, rockMost: 0.05f), lean);

            Assert.AreEqual(
                (Radius * (1f + PulseSwell + 0.08f)) + lean.MaxLean + 0.05f + (MushroomQuadRoom.ShadowReach * 1.8f), room,
                1e-5f, "the cap swollen and breathed in, leaned, rocked, and its shadow at the top of a breath");
            Assert.AreEqual(Radius * 0.07f, Room(Idle(0.15f, 0.8f, 0.05f), lean) - room, 1e-5f, "a deeper breath");
            Assert.AreEqual(0.03f, Room(Idle(0.08f, 0.8f, 0.08f), lean) - room, 1e-5f, "a further rocking");
            Assert.AreEqual(
                MushroomQuadRoom.ShadowReach * 1.2f, Room(Idle(0.08f, 2f, 0.05f), lean) - room, 1e-5f,
                "a shadow that lifts further");
        }

        [Test]
        public void TheShippedQuad_HasTheRoomTheShippedNumbersAskFor_AndARetunePastItWouldNot()
        {
            var quad = AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(quad, $"{QuadPath} should load");
            Assert.IsNotNull(material, $"{MaterialPath} should load");
            var pulse = material.GetFloat("_PulseSwell");

            var needed = MushroomQuadRoom.Half(Shipped.Radius, pulse, Shipped.Lean, Shipped.Idle, Shipped.Cut);

            Assert.GreaterOrEqual(quad.bounds.extents.x, needed - 1e-4f,
                "the shader never draws past the mesh's corner: rebuild the prefab after a retune");
            Assert.GreaterOrEqual(quad.bounds.extents.y, needed - 1e-4f);

            // The same check with a number raised and the prefab not rebuilt: it is what this test is for.
            var shipped = Shipped.Idle;
            var retuned = new MushroomIdleTuning(
                shipped.Breath, shipped.BreathSeconds, shipped.PhaseSpread, shipped.WaveAcross, shipped.ShadowLift,
                shipped.GlowSwing, shipped.WartSpread, shipped.RockPerDisplacement, shipped.RockMost + 0.05f,
                shipped.RockSeconds, shipped.RockSwing);
            Assert.Greater(
                MushroomQuadRoom.Half(Shipped.Radius, pulse, Shipped.Lean, retuned, Shipped.Cut), quad.bounds.extents.x,
                "a rocking 0.05 further needs more than the shipped quad has");
        }

        private static float Room(in MushroomIdleTuning idle, in MushroomLeanTuning lean)
        {
            return MushroomQuadRoom.Half(Radius, PulseSwell, lean, idle, default);
        }

        private static MushroomIdleTuning Idle(float breath, float shadowLift, float rockMost)
        {
            return new MushroomIdleTuning(
                breath, breathSeconds: 2.6f, phaseSpread: 0.1f, waveAcross: 0.2f, shadowLift, glowSwing: 0.4f,
                wartSpread: 0.08f, rockPerDisplacement: 0.16f, rockMost, rockSeconds: 0.45f, rockSwing: 0.6f);
        }

        private static MushroomIdleTuning Tuning(float breath, float breathSeconds, float phaseSpread, float waveAcross)
        {
            return new MushroomIdleTuning(
                breath, breathSeconds, phaseSpread, waveAcross, shadowLift: 0.5f, glowSwing: 0.4f, wartSpread: 0.05f,
                rockPerDisplacement: 0.1f, rockMost: 0.05f, rockSeconds: 0.45f, rockSwing: 0.6f);
        }
    }
}
