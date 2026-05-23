using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class HitableTests
    {
        [Test]
        public void HitOutcome_AbsorbVariantExists()
        {
            Assert.AreNotEqual(HitOutcome.Absorb, HitOutcome.Deflect);
            Assert.AreNotEqual(HitOutcome.Absorb, HitOutcome.Pop);
            Assert.AreNotEqual(HitOutcome.Absorb, HitOutcome.PassThrough);
        }

        [Test]
        public void IndestructibleAbsorbingActor_IsIHitable_NotIHasDurability()
        {
            var actor = new AbsorbWall();

            Assert.IsTrue(actor is IHitable);
            Assert.IsFalse(actor is IHasDurability);
        }

        [Test]
        public void IndestructibleAbsorbingActor_EvaluateHit_ReturnsAbsorb()
        {
            var actor = new AbsorbWall();

            Assert.AreEqual(HitOutcome.Absorb, actor.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void NonDeflectingActor_EvaluateHit_AlwaysReturnsPop_AndDecrementsHits()
        {
            var actor = new NonDeflectingActor(3);

            Assert.AreEqual(HitOutcome.Pop, actor.EvaluateHit(new DamageContext(1)));
            Assert.AreEqual(2, ((IHasDurability)actor).HitsRemaining.Value);
        }

        [Test]
        public void NonDeflectingActor_HitsRemainingReachesZero_OnFinalHit()
        {
            var actor = new NonDeflectingActor(1);

            actor.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(0, ((IHasDurability)actor).HitsRemaining.Value);
        }

        [Test]
        public void HitRouting_IHitableWithoutIHasDurability_RemovalCheckSkipped()
        {
            ISlotActor actor = new AbsorbWall();

            var isDurable = actor is IHasDurability;

            Assert.IsFalse(isDurable);
        }

        private class AbsorbWall : ISlotActor, IHitable
        {
            public Vector2Int SlotIndex => default;
            public SlotActorKind Kind => SlotActorKind.Static;
            public HitOutcome EvaluateHit(DamageContext context) => HitOutcome.Absorb;
        }

        private class NonDeflectingActor : ISlotActor, IHasDurability
        {
            private readonly ReactiveProperty<int> _hits;

            public NonDeflectingActor(int hits) => _hits = new ReactiveProperty<int>(hits);

            public Vector2Int SlotIndex => default;
            public SlotActorKind Kind => SlotActorKind.Static;
            public IReadOnlyReactiveProperty<int> HitsRemaining => _hits;

            public HitOutcome EvaluateHit(DamageContext context)
            {
                _hits.Value -= context.Damage;
                return HitOutcome.Pop;
            }
        }
    }
}

