using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     What a mushroom's cap does once a lance has cut it (<see cref="MushroomCutTimeline" />,
    ///     @ref plan_meadow_mushrooms): its two pieces part quickly, then drop and fade; the stem is left bare;
    ///     and the cap pops back. Every value is a matter of the time passed, so it is the same at any frame rate
    ///     and ends exactly at rest.
    /// </summary>
    [TestFixture]
    public class MushroomCutTimelineTests
    {
        private const float Frame = 1f / 60f;

        private static readonly MushroomCutTuning Shipped = new MushroomSettings().Cut;

        [Test]
        public void AFreshTimeline_IsAWholeCapAtRest()
        {
            var timeline = new MushroomCutTimeline();

            Assert.IsFalse(timeline.IsCut);
            Assert.IsTrue(timeline.IsAtRest);
            Assert.AreEqual(1f, timeline.CapGrow);
            Assert.AreEqual(0f, timeline.PieceFade, "no pieces: the shader skips their work");
            Assert.IsFalse(timeline.Step(Frame, Shipped));
        }

        [Test]
        public void ACut_TakesTheCapOffAtOnce_AndItsPiecesStartWhereItWas()
        {
            var timeline = new MushroomCutTimeline();

            timeline.Cut();

            Assert.IsTrue(timeline.IsCut);
            Assert.IsFalse(timeline.IsAtRest);
            Assert.AreEqual(0f, timeline.CapGrow, "the whole cap is gone from the stem");
            Assert.AreEqual(1f, timeline.PieceFade, "and its pieces are fully there");
            Assert.AreEqual(0f, timeline.Part(Shipped), "not yet apart, so nothing jumps");
            Assert.AreEqual(0f, timeline.Carry(Shipped));
            Assert.AreEqual(0f, timeline.Turn(Shipped));
            Assert.AreEqual(1f, timeline.PieceSize(Shipped));
            Assert.AreEqual(1f, timeline.PieceHeight);
        }

        [Test]
        public void ThePiecesPartQuicklyAtFirst_AndOnlyEverFurther()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();

            var last = 0f;
            var atAThird = 0f;
            var elapsed = 0f;
            while (timeline.Step(Frame, Shipped))
            {
                elapsed += Frame;
                var part = timeline.Part(Shipped);
                Assert.GreaterOrEqual(part, last - 1e-6f, "a piece never comes back toward the cut");
                last = part;
                if (atAThird == 0f && elapsed >= Shipped.FallSeconds / 3f)
                {
                    atAThird = part;
                }
            }

            Assert.Greater(atAThird, Shipped.PartDistance * 0.65f, "a clean slice opens in the first third");
            Assert.AreEqual(Shipped.PartDistance, last, Shipped.PartDistance * 0.05f, "and ends as far as tuned");
        }

        [Test]
        public void ThePiecesOpenBeforeTheyDrop_ThenShrinkAndSinkAndFade()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();

            Advance(ref timeline, Shipped.FallSeconds * 0.25f, Frame);
            Assert.AreEqual(1f, timeline.PieceSize(Shipped), "still at the cap's height: the slice is seen open");
            Assert.AreEqual(1f, timeline.PieceHeight);
            Assert.AreEqual(1f, timeline.PieceFade);
            Assert.Greater(timeline.Turn(Shipped), 0f);
            Assert.Greater(timeline.Carry(Shipped), 0f);

            Advance(ref timeline, Shipped.FallSeconds * 0.7f, Frame);
            Assert.Less(timeline.PieceSize(Shipped), 0.75f, "nearly on the ground it is smaller");
            Assert.GreaterOrEqual(timeline.PieceSize(Shipped), Shipped.EndSize - 1e-4f);
            Assert.Less(timeline.PieceHeight, 0.2f, "and its shadow has closed in under it");
            Assert.Less(timeline.PieceFade, 0.4f, "and it has all but faded");
            Assert.Greater(timeline.PieceFade, 0f);
        }

        [Test]
        public void TheFallEnds_OnABareStem_ExactlyAtRest()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();

            var frames = 0;
            while (timeline.Step(Frame, Shipped))
            {
                frames++;
                Assert.Less(frames, 600, "still falling after ten seconds");
            }

            Assert.AreEqual(Shipped.FallSeconds, frames * Frame, 2f * Frame, "it takes the tuned time");
            Assert.IsTrue(timeline.IsCut, "the cap stays off until it is regrown");
            Assert.IsTrue(timeline.IsAtRest);
            Assert.AreEqual(0f, timeline.CapGrow);
            Assert.AreEqual(0f, timeline.PieceFade, "the pieces are gone, and their work with them");
            Assert.AreEqual(0f, timeline.Part(Shipped));
            Assert.AreEqual(0f, timeline.Carry(Shipped));
            Assert.AreEqual(0f, timeline.Turn(Shipped));
            Assert.AreEqual(1f, timeline.PieceSize(Shipped));
            Assert.IsFalse(timeline.Step(Frame, Shipped), "and it stays there");
        }

        [TestCase(1f / 120f)]
        [TestCase(1f / 30f)]
        [TestCase(1f / 12f)]
        public void TheFallIsTheSame_WhateverTheFrameRate(float frame)
        {
            var reference = new MushroomCutTimeline();
            reference.Cut();
            reference.Step(0.25f, Shipped);

            var timeline = new MushroomCutTimeline();
            timeline.Cut();
            Advance(ref timeline, 0.25f, frame);

            Assert.AreEqual(reference.Part(Shipped), timeline.Part(Shipped), 1e-4f);
            Assert.AreEqual(reference.Turn(Shipped), timeline.Turn(Shipped), 1e-4f);
            Assert.AreEqual(reference.PieceSize(Shipped), timeline.PieceSize(Shipped), 1e-4f);
            Assert.AreEqual(reference.PieceFade, timeline.PieceFade, 1e-3f);
        }

        [Test]
        public void AHitch_EndsTheFallAtRest()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();

            Assert.IsFalse(timeline.Step(5f, Shipped));

            Assert.IsTrue(timeline.IsCut);
            Assert.AreEqual(0f, timeline.PieceFade);
        }

        [Test]
        public void TheCapPopsBack_PastItsSizeByAboutTheOvershoot_AndEndsExactlyWhole()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();
            timeline.Step(5f, Shipped);

            timeline.Regrow();
            Assert.IsFalse(timeline.IsCut, "from the regrow on the mushroom is no longer cut");
            Assert.IsFalse(timeline.IsAtRest);
            Assert.AreEqual(0f, timeline.CapGrow, "it grows from nothing");

            var largest = 0f;
            var frames = 0;
            while (timeline.Step(1f / 240f, Shipped))
            {
                frames++;
                largest = Mathf.Max(largest, timeline.CapGrow);
            }

            Assert.AreEqual(1f + Shipped.RegrowOvershoot, largest, 0.01f);
            Assert.AreEqual(Shipped.RegrowSeconds, frames / 240f, 0.01f);
            Assert.AreEqual(1f, timeline.CapGrow, "exactly whole");
            Assert.IsTrue(timeline.IsAtRest);
            Assert.IsFalse(timeline.Step(Frame, Shipped));
        }

        [Test]
        public void WithNoOvershoot_TheCapNeverPassesItsSize()
        {
            var plain = new MushroomCutTuning(0.6f, 0.16f, 0.08f, 14f, 0.62f, 0.35f, 0f, Shipped.Blown);
            var timeline = new MushroomCutTimeline();
            timeline.Cut();
            timeline.Step(5f, plain);
            timeline.Regrow();

            while (timeline.Step(1f / 240f, plain))
            {
                Assert.LessOrEqual(timeline.CapGrow, 1f + 1e-5f);
            }
        }

        [Test]
        public void ARegrowOfAWholeCap_DoesNothing()
        {
            var timeline = new MushroomCutTimeline();

            timeline.Regrow();

            Assert.IsTrue(timeline.IsAtRest);
            Assert.AreEqual(1f, timeline.CapGrow);
        }

        [Test]
        public void ACutWhileTheCapPopsBack_TakesItOffAgain()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();
            timeline.Step(5f, Shipped);
            timeline.Regrow();
            timeline.Step(Shipped.RegrowSeconds * 0.5f, Shipped);

            timeline.Cut();

            Assert.IsTrue(timeline.IsCut);
            Assert.AreEqual(0f, timeline.CapGrow);
            Assert.AreEqual(1f, timeline.PieceFade);
        }

        [Test]
        public void Reset_IsAWholeCap()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Cut();
            timeline.Step(Frame, Shipped);

            timeline.Reset();

            Assert.IsFalse(timeline.IsCut);
            Assert.IsTrue(timeline.IsAtRest);
            Assert.AreEqual(1f, timeline.CapGrow);
            Assert.AreEqual(0f, timeline.PieceFade);
        }

        [Test]
        public void TheShippedTuning_IsAQuickSlice_AndAShortPop()
        {
            Assert.That(Shipped.FallSeconds, Is.InRange(0.5f, 0.7f), "the owner asked for a quick fall");
            Assert.That(Shipped.RegrowSeconds, Is.InRange(0.25f, 0.45f));
            Assert.Less(Shipped.EndSize, 1f, "a piece that falls gets smaller, seen from above");
            Assert.Greater(Shipped.PartDistance, 0.1f, "the slice opens wide enough to see the stem between");
        }

        [Test]
        public void TheRoomTheQuadKeepsForThePieces_IsTheShadersBound_NotTheShorterTrueReach()
        {
            const float radius = 0.3f;

            // The shader's vertex stage boxes the pieces by the cap's radius grown by the turn, plus the part and
            // the carry summed: the mesh has to hold that box, or the box is cut short at the mesh's edge.
            // Held for a cut alone: a tuning whose blast throws nothing (the throw's own room is held below).
            var cutOnly = new MushroomCutTuning(0.6f, 0.16f, 0.08f, 14f, 0.62f, 0.35f, 0f, default);
            var shaderBound = (radius * cutOnly.TurnRadians) + cutOnly.PartDistance + cutOnly.CarryDistance;

            Assert.AreEqual(shaderBound, cutOnly.RoomBeyond(radius), 1e-6f);
            Assert.Greater(
                cutOnly.RoomBeyond(radius),
                Mathf.Sqrt((cutOnly.PartDistance * cutOnly.PartDistance) + (cutOnly.CarryDistance * cutOnly.CarryDistance)),
                "more than how far a piece's middle truly gets");
            Assert.AreEqual(cutOnly.PartDistance + cutOnly.CarryDistance, cutOnly.RoomBeyond(-1f), 1e-6f);
            Assert.GreaterOrEqual(Shipped.RoomBeyond(radius), shaderBound, "the shipped quad holds a cut's pieces too");
        }

        [Test]
        public void ABlast_TakesTheCapOffAtOnce_Whole_AndItStartsWhereItWas()
        {
            var timeline = new MushroomCutTimeline();

            timeline.Blow();

            Assert.IsTrue(timeline.IsCut, "the stem is bare, as after a lance");
            Assert.IsTrue(timeline.IsBlown, "and what came off is one piece");
            Assert.IsFalse(timeline.IsAtRest);
            Assert.AreEqual(0f, timeline.CapGrow);
            Assert.AreEqual(1f, timeline.PieceFade);
            Assert.AreEqual(0f, timeline.Part(Shipped), "not yet thrown, so nothing jumps");
            Assert.AreEqual(0f, timeline.Turn(Shipped));
            Assert.AreEqual(1f, timeline.PieceSize(Shipped));
            Assert.AreEqual(1f, timeline.ShadowHeight(Shipped), "its shadow where the cap's was");
            Assert.AreEqual(0f, timeline.Arc);
        }

        [Test]
        public void ABlownCap_IsThrownFurtherThanACutPieceParts_FastAtFirst_AndOnlyEverFurther()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Blow();

            var last = 0f;
            var atAThird = 0f;
            var elapsed = 0f;
            while (timeline.Step(Frame, Shipped))
            {
                elapsed += Frame;
                var thrown = timeline.Part(Shipped);
                Assert.GreaterOrEqual(thrown, last - 1e-6f, "a thrown cap never comes back");
                Assert.AreEqual(0f, timeline.Carry(Shipped), "it goes the one way, away from the blast");
                last = thrown;
                if (atAThird == 0f && elapsed >= Shipped.Blown.Seconds / 3f)
                {
                    atAThird = thrown;
                }
            }

            Assert.Greater(atAThird, Shipped.Blown.Distance * 0.5f, "most of the way while it is still rising");
            Assert.AreEqual(Shipped.Blown.Distance, last, Shipped.Blown.Distance * 0.05f);
            Assert.Greater(Shipped.Blown.Distance, 2f * Shipped.PartDistance, "it was hit by a bomb");
        }

        [Test]
        public void ABlownCap_IsLiftedFirst_ThenLands_ShrinksAndFades()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Blow();

            Advance(ref timeline, Shipped.Blown.Seconds * 0.35f, Frame);
            Assert.Greater(timeline.Arc, 0.9f, "near the top of its throw early, as a thing thrown up is");
            Assert.Greater(timeline.PieceSize(Shipped), 1f + (Shipped.Blown.Lift * 0.9f), "nearer the eye: larger");
            Assert.Greater(timeline.ShadowHeight(Shipped), 1.5f, "and its shadow well off it");
            Assert.AreEqual(1f, timeline.PieceFade, "whole while it rises");
            Assert.Greater(timeline.Turn(Shipped), 0f, "tumbling");

            Advance(ref timeline, Shipped.Blown.Seconds * 0.6f, Frame);
            Assert.Less(timeline.PieceSize(Shipped), 0.75f, "on the ground it is smaller than it was on the stem");
            Assert.Less(timeline.ShadowHeight(Shipped), 0.3f, "its shadow has closed in under it");
            Assert.Less(timeline.PieceFade, 0.3f, "and it has all but faded");
            Assert.Greater(timeline.PieceFade, 0f);
        }

        [TestCase(1f / 120f)]
        [TestCase(1f / 30f)]
        [TestCase(1f / 12f)]
        public void TheThrowIsTheSame_WhateverTheFrameRate_AndEndsOnABareStemAtRest(float frame)
        {
            var reference = new MushroomCutTimeline();
            reference.Blow();
            reference.Step(0.25f, Shipped);

            var timeline = new MushroomCutTimeline();
            timeline.Blow();
            Advance(ref timeline, 0.25f, frame);

            Assert.AreEqual(reference.Part(Shipped), timeline.Part(Shipped), 1e-4f);
            Assert.AreEqual(reference.PieceSize(Shipped), timeline.PieceSize(Shipped), 1e-4f);
            Assert.AreEqual(reference.ShadowHeight(Shipped), timeline.ShadowHeight(Shipped), 1e-3f);

            var frames = Mathf.RoundToInt(0.25f / frame);
            while (timeline.Step(frame, Shipped))
            {
                frames++;
                Assert.Less(frames, 1200, "still flying");
            }

            Assert.AreEqual(Shipped.Blown.Seconds, frames * frame, 2f * frame, "it takes the tuned time");
            Assert.IsTrue(timeline.IsCut);
            Assert.IsFalse(timeline.IsBlown);
            Assert.IsTrue(timeline.IsAtRest);
            Assert.AreEqual(0f, timeline.PieceFade);
            Assert.AreEqual(0f, timeline.Part(Shipped));
            Assert.AreEqual(1f, timeline.PieceSize(Shipped));
            Assert.AreEqual(1f, timeline.ShadowHeight(Shipped));
        }

        [Test]
        public void ABlownCap_GrowsBackAsACutOneDoes()
        {
            var timeline = new MushroomCutTimeline();
            timeline.Blow();
            timeline.Step(5f, Shipped);

            timeline.Regrow();
            Assert.IsFalse(timeline.IsCut);
            while (timeline.Step(Frame, Shipped))
            {
            }

            Assert.AreEqual(1f, timeline.CapGrow);
            Assert.IsTrue(timeline.IsAtRest);
        }

        [Test]
        public void TheQuadsRoom_HoldsTheThrow_ByTheShadersBound()
        {
            const float radius = 0.3f;
            var blown = Shipped.Blown;

            // The shader boxes a piece by the cap's radius grown by its lift and its turn, plus how far it is
            // moved: for a blown cap that is more than a cut's pieces need, and the quad is sized for the larger.
            var throwBound = (radius * (blown.Lift + blown.TurnRadians)) + blown.Distance;

            Assert.AreEqual(throwBound, Shipped.RoomBeyond(radius), 1e-6f);
            Assert.Greater(
                throwBound, (radius * Shipped.TurnRadians) + Shipped.PartDistance + Shipped.CarryDistance);
            Assert.AreEqual(blown.ShadowLift, Shipped.ShadowLift, 1e-6f, "and the shadow's room for the lift");
            Assert.Greater(Shipped.ShadowLift, 1f);
        }

        private static void Advance(ref MushroomCutTimeline timeline, float seconds, float frame)
        {
            var steps = Mathf.RoundToInt(seconds / frame);
            for (var i = 0; i < steps; i++)
            {
                timeline.Step(frame, Shipped);
            }

            timeline.Step(seconds - (steps * frame), Shipped);
        }
    }
}
