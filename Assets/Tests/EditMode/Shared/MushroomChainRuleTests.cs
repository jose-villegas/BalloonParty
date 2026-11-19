using System;
using System.Linq;
using System.Reflection;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     The truth table of the mushroom chain (<see cref="MushroomChainRule" />): which bounce arms the lance,
    ///     that nothing is paid before or after it, what a wall does, and that the wall of caps hit last does not
    ///     count again until another one has been hit. The rule is only asked about a ball that is not a lance:
    ///     a lance bounces off no mushroom.
    /// </summary>
    [TestFixture]
    public class MushroomChainRuleTests
    {
        private const int A = 0;
        private const int B = 1;
        private const int C = 2;
        private const int D = 3;

        // The rule as settled: 3 hits arm.
        private static readonly MushroomChainRules Settled = new(chainTarget: 3);

        [Test]
        public void OnBounce_ThirdMushroom_ArmsAndNotBefore()
        {
            var state = default(MushroomChainState);

            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, A, Settled));
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, B, Settled));
            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, C, Settled));
            Assert.AreEqual(3, state.Count);
            Assert.AreEqual(C, state.LastWall);
        }

        // Decision J: no charge is banked. A ball armed at the target is a lance and bounces off nothing, so a
        // hit past the target is only reached by a ball whose arming never landed, and it pays nothing.
        [Test]
        public void OnBounce_HitsPastTheTarget_CountAndPayNothing()
        {
            var state = Bounced(Settled, A, B, C);

            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, D, Settled));
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, A, Settled));
            Assert.AreEqual(5, state.Count, "they still count; they only pay nothing");
        }

        [Test]
        public void TheRule_AnswersABounceThatPaysNothing_TheArming_OrNoBounce_AndNothingElse()
        {
            var answered = Enum.GetValues(typeof(MushroomReward)).Cast<MushroomReward>()
                .Where(reward => Answers(reward)).ToArray();

            CollectionAssert.AreEquivalent(
                new[] { MushroomReward.None, MushroomReward.Arm, MushroomReward.PassThrough }, answered);
        }

        [Test]
        public void OnBounce_SameMushroomTwiceInARow_CountsOnce()
        {
            var state = Bounced(Settled, A);

            var reward = MushroomChainRule.OnBounce(ref state, A, Settled);

            Assert.AreEqual(MushroomReward.None, reward);
            Assert.AreEqual(1, state.Count);
            Assert.AreEqual(A, state.LastWall);
        }

        // The rule is told the wall a cap belongs to, not the cap: two caps of one wall are the same number to it.
        [Test]
        public void OnBounce_CapsOfOneWall_CountOnce_AndACapOfAnotherWallCounts()
        {
            var caps = new[]
            {
                new BumperCircle(0, 0, Vector2.zero, 0.425f), new BumperCircle(1, 0, new Vector2(0.75f, 0f), 0.425f),
                new BumperCircle(2, 1, new Vector2(0f, 2f), 0.425f),
            };
            var state = default(MushroomChainState);

            // A rattle along the first wall, across to the second, and back to the first's other cap.
            var rewards = new[] { 0, 1, 0, 2, 1 }
                .Select(cap => MushroomChainRule.OnBounce(ref state, caps[cap].Wall, Settled)).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    MushroomReward.None, MushroomReward.None, MushroomReward.None, MushroomReward.None, MushroomReward.Arm,
                },
                rewards);
            Assert.AreEqual(3, state.Count, "one hit a wall: the rattle was worth one");
            Assert.AreEqual(5, state.Bounces, "though every bounce is tallied against the cap");
            Assert.AreEqual(0, state.LastWall);
        }

        // A wall keeps its number with caps missing: the cap left of a wall whose other cap a lance cut counts as
        // that wall still.
        [Test]
        public void OnBounce_TheCapLeftOfAWallWithACapCut_StillCountsAsItsWall()
        {
            var left = new BumperCircle(1, 0, new Vector2(0.75f, 0f), 0.425f);
            var other = new BumperCircle(2, 1, new Vector2(0f, 2f), 0.425f);
            var state = default(MushroomChainState);

            MushroomChainRule.OnBounce(ref state, left.Wall, Settled);
            MushroomChainRule.OnBounce(ref state, other.Wall, Settled);

            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, left.Wall, Settled));
        }

        [Test]
        public void ACapByItself_IsItsOwnWall()
        {
            Assert.AreEqual(4, new BumperCircle(4, Vector2.zero, 0.425f).Wall);
            Assert.AreEqual(1, new BumperCircle(4, 1, Vector2.zero, 0.425f).Wall);
        }

        [Test]
        public void OnBounce_SameMushroomAfterAnother_CountsAgain_SoTwoMushroomsArm()
        {
            var state = Bounced(Settled, A, B);

            var reward = MushroomChainRule.OnBounce(ref state, A, Settled);

            Assert.AreEqual(MushroomReward.Arm, reward, "A, B, A is three hits");
            Assert.AreEqual(3, state.Count);
        }

        [Test]
        public void OnBounce_RepeatsBetweenHits_NeverReachTheTarget()
        {
            var state = Bounced(Settled, A, A, A, B, B);

            Assert.AreEqual(2, state.Count);
        }

        [Test]
        public void OnBounce_DefaultState_CountsMushroomZero()
        {
            var state = default(MushroomChainState);

            Assert.AreEqual(MushroomChainState.NoBumper, state.LastWall);
            MushroomChainRule.OnBounce(ref state, 0, Settled);

            Assert.AreEqual(1, state.Count, "Id 0 must not read as the mushroom hit last on a fresh flight");
        }

        [Test]
        public void OnWall_ResetsTheCountAndTheLastMushroom()
        {
            var state = Bounced(Settled, A, B);

            MushroomChainRule.OnWall(ref state);

            Assert.AreEqual(0, state.Count);
            Assert.AreEqual(MushroomChainState.NoBumper, state.LastWall);
        }

        [Test]
        public void OnWall_BeforeTheThirdHit_TheChainStartsOver()
        {
            var state = Bounced(Settled, A, B);
            MushroomChainRule.OnWall(ref state);

            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, C, Settled));
            Assert.AreEqual(1, state.Count);
        }

        [Test]
        public void OnWall_TheMushroomHitBeforeIt_CountsAgainAfterIt()
        {
            var state = Bounced(Settled, A, B);
            MushroomChainRule.OnWall(ref state);

            MushroomChainRule.OnBounce(ref state, B, Settled);

            Assert.AreEqual(1, state.Count);
        }

        // A lance spent at a wall is an ordinary ball again and bounces off the mushrooms again: the wall has
        // cleared its chain, and a new chain arms it again. The rule caps no armings.
        [Test]
        public void OnBounce_AfterAWall_ANewChainArmsAgain_AsOftenAsItIsFlown()
        {
            var state = default(MushroomChainState);
            var armed = 0;

            for (var chain = 0; chain < 6; chain++)
            {
                foreach (var wall in new[] { A, B, C })
                {
                    armed += MushroomChainRule.OnBounce(ref state, wall, Settled) == MushroomReward.Arm ? 1 : 0;
                }

                MushroomChainRule.OnWall(ref state);
            }

            Assert.AreEqual(6, armed);
        }

        [Test]
        public void ChainTarget_ComesFromTheRules()
        {
            var rules = new MushroomChainRules(chainTarget: 2);
            var state = Bounced(rules, A);

            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, B, rules));
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, A, rules));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ChainTarget_ZeroOrLess_PaysNothing(int target)
        {
            var rules = new MushroomChainRules(target);
            var state = default(MushroomChainState);

            foreach (var id in new[] { A, B, C, D, A })
            {
                Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, id, rules));
            }
        }

        [Test]
        public void RepeatHitCounts_On_TheSameMushroomArmsByItself()
        {
            var rules = new MushroomChainRules(chainTarget: 3, repeatHitCounts: true);
            var state = Bounced(rules, A, A);

            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, A, rules));
        }

        [Test]
        public void OnBounce_TalliesEveryBounce_TheOnesThatDoNotCountIncluded()
        {
            var state = Bounced(Settled, A, A, B);

            Assert.AreEqual(3, state.Bounces);
            Assert.AreEqual(2, state.Count);
        }

        [Test]
        public void OnBounce_PastTheCap_PassesThroughAndChangesNothing()
        {
            var rules = new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: 3);
            var state = Bounced(rules, A, B, C);

            Assert.AreEqual(MushroomReward.PassThrough, MushroomChainRule.OnBounce(ref state, D, rules));
            Assert.AreEqual(3, state.Bounces);
            Assert.AreEqual(3, state.Count);
            Assert.AreEqual(C, state.LastWall);
        }

        // The loop the cap is there for: a ricochet off an unbreakable sends the ball back to the mushroom it just
        // left, a hit that does not count, is kicked again and could repeat for good.
        [Test]
        public void OnBounce_TheSameMushroomOverAndOver_EndsAtTheCap()
        {
            var state = default(MushroomChainState);
            var bounces = 0;
            while (bounces < 1000 && MushroomChainRule.OnBounce(ref state, A, Settled) != MushroomReward.PassThrough)
            {
                bounces++;
            }

            Assert.AreEqual(Settled.MaxBouncesPerStretch, bounces);
            Assert.AreEqual(1, state.Count);
        }

        [Test]
        public void IsSpent_OnlyOnceTheStretchHasHadItsBounces()
        {
            var rules = new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: 2);
            var state = Bounced(rules, A);

            Assert.IsFalse(MushroomChainRule.IsSpent(state, rules));
            MushroomChainRule.OnBounce(ref state, B, rules);

            Assert.IsTrue(MushroomChainRule.IsSpent(state, rules));
        }

        [Test]
        public void OnWall_ClearsTheBounces_SoTheMushroomsBounceTheBallAgain()
        {
            var rules = new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: 2);
            var state = Bounced(rules, A, B);
            Assert.AreEqual(MushroomReward.PassThrough, MushroomChainRule.OnBounce(ref state, C, rules));

            MushroomChainRule.OnWall(ref state);

            Assert.AreEqual(0, state.Bounces);
            Assert.AreEqual(MushroomReward.None, MushroomChainRule.OnBounce(ref state, C, rules));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void MaxBouncesPerStretch_ZeroOrLess_ReadsAsTheDefault_NeverAsNoCap(int authored)
        {
            var rules = new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: authored);

            Assert.AreEqual(MushroomChainRules.DefaultMaxBouncesPerStretch, rules.MaxBouncesPerStretch);
            Assert.AreEqual(
                MushroomChainRules.DefaultMaxBouncesPerStretch, default(MushroomChainRules).MaxBouncesPerStretch);
        }

        [Test]
        public void MaxBouncesPerStretch_AsShipped_LeavesRoomToBeArmed()
        {
            var state = default(MushroomChainState);

            MushroomChainRule.OnBounce(ref state, A, Settled);
            MushroomChainRule.OnBounce(ref state, B, Settled);
            Assert.AreEqual(MushroomReward.Arm, MushroomChainRule.OnBounce(ref state, C, Settled));
            Assert.Less(state.Bounces, Settled.MaxBouncesPerStretch);
        }

        // A balloon pop, a ricochet off an unbreakable and a cut cap leave the count alone. The rule keeps that by
        // having no call for them: the only two that write a flight's state are the bounce and the wall.
        [Test]
        public void PopRicochetAndCut_HaveNoCall_OnlyTheBounceAndTheWallWriteTheState()
        {
            var writers = typeof(MushroomChainRule)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Where(method => method.GetParameters().Any(WritesTheState))
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { nameof(MushroomChainRule.OnBounce), nameof(MushroomChainRule.OnWall) }, writers);
        }

        // Whether some flight of up to six bounces over three walls, at some cap on bounces, gets the answer.
        private static bool Answers(MushroomReward wanted)
        {
            foreach (var cap in new[] { 2, 12 })
            {
                var rules = new MushroomChainRules(chainTarget: 3, maxBouncesPerStretch: cap);
                for (var flight = 0; flight < 729; flight++)
                {
                    var state = default(MushroomChainState);
                    var walls = flight;
                    for (var bounce = 0; bounce < 6; bounce++, walls /= 3)
                    {
                        if (MushroomChainRule.OnBounce(ref state, walls % 3, rules) == wanted)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool WritesTheState(ParameterInfo parameter)
        {
            return parameter.ParameterType.IsByRef
                && !parameter.IsIn
                && parameter.ParameterType.GetElementType() == typeof(MushroomChainState);
        }

        private static MushroomChainState Bounced(MushroomChainRules rules, params int[] ids)
        {
            var state = default(MushroomChainState);
            foreach (var id in ids)
            {
                MushroomChainRule.OnBounce(ref state, id, rules);
            }

            return state;
        }
    }
}
