using System.Collections;
using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Regression test for the whole teleport/drift/stuck bug class that PLAN-NudgeLayeredMotion
    ///     replaces guard-based coordination with: publishes nudges at balloons that are actively
    ///     mid-balance-move (and their neighbors) — the exact overlap the old guard/deferral system
    ///     existed to prevent — then asserts every balloon settles exactly on its slot once the board
    ///     stabilizes. Under the new impulse-stack model the two motion systems are orthogonal, so the
    ///     overlap should be harmless.
    /// </summary>
    public class NudgeSettlePlayModeTests : PlayModeGameTest
    {
        [TearDown]
        public void ResetCheats()
        {
            Cheats.CheatState.BlockLevelUp = false;
        }

        [UnityTest]
        public IEnumerator NudgeDuringBalance_EveryBalloonSettlesExactlyOnSlot()
        {
            yield return LoadGameScene();

            // Extra spawned lines on a filling board publish SpawnBlockedMessage per rejected column,
            // draining hit points — enough rejects lose the run, and the loss→restart flow parks the
            // whole incoming board below its slots (IsStable false) waiting on the restart rise. The
            // level-lock cheat makes hearts undrainable so this test only ever observes nudge/balance
            // motion, not the loss cinematic.
            Cheats.CheatState.BlockLevelUp = true;

            var grid = Resolve<SlotGrid>();
            var linePublisher = Resolve<IPublisher<SpawnBalloonLineMessage>>();
            var nudgePublisher = Resolve<IPublisher<NudgeMessage>>();
            var transit = Resolve<BalancePathHolder>();
            var pause = Resolve<PauseService>();

            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned.");

            for (var i = 0; i < 3; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            yield return WaitUntil(() => !pause.IsAnyPaused.Value, message: "Overflow hold never released.");
            yield return WaitUntil(() => !AnyInTransit(transit, grid), message: "Spawn wave never settled.");

            // Line spawns often place already-balanced, producing no balance moves at all — pop a
            // spread of interior balloons instead. The holes guarantee real balance slides (neighbors
            // float up into them), and the pops themselves fire genuine neighbor nudges through
            // NudgeService, exactly like live play.
            var popped = PopInteriorBalloons(grid, count: 5);
            Assert.Greater(popped, 0, "No interior balloon available to pop — board never filled.");
            Resolve<IPublisher<BalanceBalloonsMessage>>().Publish(default);

            yield return WaitUntil(() => AnyInTransit(transit, grid),
                message: "Popping holes never produced a balance move.");

            // Nudge every in-transit balloon and its neighbors for as long as a balance move stays in
            // flight — the exact overlap (nudge impulse concurrent with a base-position DOPath) the old
            // guard/deferral system paused around and the new layering claims is simply harmless.
            var nudgedAny = false;
            var frame = 0;
            while (AnyInTransit(transit, grid) && frame < 90)
            {
                if (NudgeInTransitBalloonsAndNeighbors(grid, transit, nudgePublisher))
                {
                    nudgedAny = true;
                }

                frame++;
                yield return null;
            }

            yield return WaitUntil(() => !AnyInTransit(transit, grid),
                message: "Board never settled after nudges fired during balance.");

            // Transit only covers balance moves — spawn path tweens run outside BalancePathHolder,
            // so also wait for every actor's IsStable (set by the spawn/balance OnComplete).
            var stableTimeout = Time.realtimeSinceStartup + DefaultTimeout;
            while (!AllStable(grid) && Time.realtimeSinceStartup < stableTimeout)
            {
                yield return null;
            }

            Assert.IsTrue(AllStable(grid),
                $"Actors never reported IsStable after settling: {DescribeUnstable(grid, transit)}");

            // IsStable deliberately ignores nudges under the layered model, so impulses fired on the
            // last in-transit frame may still be decaying — poll until every view converges onto its
            // slot, then assert for the readable per-balloon failure message.
            yield return WaitUntil(() => EveryBalloonOnSlot(grid, 1e-3f),
                message: "Balloons never converged onto their slots after impulses decayed.");

            AssertEveryBalloonExactlyOnSlot(grid);

            Assert.IsTrue(nudgedAny,
                "No balance move stayed in transit long enough to nudge during it, even after popping holes.");
        }

        // Pops up to count occupied interior balloons, spread out so the holes create independent
        // balance slides — through the real hit pipeline, mirroring BalloonRemoverCheat's removal path.
        private static int PopInteriorBalloons(SlotGrid grid, int count)
        {
            var dispatcher = Resolve<IHitDispatcher>();
            var popped = 0;

            for (var col = 1; col < grid.Columns - 1 && popped < count; col += 2)
            {
                for (var row = 1; row < grid.Rows - 1 && popped < count; row += 3)
                {
                    var slot = new Vector2Int(col, row);
                    var actor = grid.At(slot);
                    if (actor == null)
                    {
                        continue;
                    }

                    dispatcher.Dispatch(new ActorHitMessage(actor,
                        grid.IndexToWorldPosition(slot),
                        Vector3.zero,
                        HitOutcome.Pop,
                        new DamageContext(1)));
                    popped++;
                }
            }

            return popped;
        }

        // Publishes a nudge at every balloon currently mid-balance-move and at its grid neighbors —
        // mirroring what NudgeService.OnActorHit/OnNudge does for a real hit, without routing through
        // the hit dispatcher (ActorHitMessage is never published directly; NudgeMessage is the direct,
        // dispatcher-free surface NudgeService also consumes).
        private static bool NudgeInTransitBalloonsAndNeighbors(
            SlotGrid grid, BalancePathHolder transit, IPublisher<NudgeMessage> publisher)
        {
            var nudgedAny = false;
            var neighbors = new List<IWriteableSlotActor>();

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (!transit.IsInTransit(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    if (grid.At(slot) is not IHasNudge target)
                    {
                        continue;
                    }

                    var origin = grid.IndexToWorldPosition(slot) + Vector3.down;
                    publisher.Publish(new NudgeMessage(target, origin, NudgeType.Deflect));
                    nudgedAny = true;

                    grid.GetNeighbors(col, row, neighbors);
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor is IHasNudge neighborTarget)
                        {
                            publisher.Publish(new NudgeMessage(neighborTarget, origin, NudgeType.Neighbor));
                        }
                    }
                }
            }

            return nudgedAny;
        }

        // Names every stuck actor with the state needed to diagnose a lost IsStable restore:
        // which type, where its view actually is vs its slot, and whether transit still holds it.
        private static string DescribeUnstable(SlotGrid grid, BalancePathHolder transit)
        {
            var parts = new List<string>();
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (grid.At(slot) is not IDynamicSlotActor { IsStable: { Value: false } } actor)
                    {
                        continue;
                    }

                    var view = grid.ViewAt(slot);
                    var viewPos = view != null ? view.transform.position.ToString("F2") : "<no view>";
                    parts.Add($"{actor.GetType().Name}@{slot} view={viewPos} " +
                              $"slotPos={grid.IndexToWorldPosition(slot):F2} " +
                              $"inTransit={transit.IsInTransit(col, row)}");
                }
            }

            if (parts.Count == 0)
            {
                return "<none — re-stabilized after timeout>";
            }

            // A rigid whole-board offset means a common ancestor is displaced, not per-balloon motion —
            // print the first stuck view's parent chain so the culprit root names itself.
            return $"{string.Join("; ", parts)} | first view's parents: {DescribeParentChain(grid)}";
        }

        private static string DescribeParentChain(SlotGrid grid)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (grid.At(slot) is not IDynamicSlotActor { IsStable: { Value: false } })
                    {
                        continue;
                    }

                    var view = grid.ViewAt(slot);
                    if (view == null)
                    {
                        continue;
                    }

                    var chain = new List<string>();
                    for (var t = view.transform; t != null; t = t.parent)
                    {
                        chain.Add($"{t.name}@{t.position:F2}(local {t.localPosition:F2})");
                    }

                    return string.Join(" <- ", chain);
                }
            }

            return "<no stuck view found>";
        }

        private static bool AllStable(SlotGrid grid)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is IDynamicSlotActor { IsStable: { Value: false } })
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool EveryBalloonOnSlot(SlotGrid grid, float tolerance)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (grid.At(slot) is not IDynamicSlotActor dynamicActor)
                    {
                        continue;
                    }

                    var view = grid.ViewAt(slot);
                    if (view == null)
                    {
                        return false;
                    }

                    var expected = grid.IndexToWorldPosition(dynamicActor.SlotIndex.Value);
                    if ((expected - view.transform.position).magnitude >= tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void AssertEveryBalloonExactlyOnSlot(SlotGrid grid)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (grid.At(slot) is not IDynamicSlotActor dynamicActor)
                    {
                        continue;
                    }

                    var view = grid.ViewAt(slot);
                    Assert.IsNotNull(view, $"Balloon at {slot} has no view.");

                    var expected = grid.IndexToWorldPosition(dynamicActor.SlotIndex.Value);
                    var actual = view.transform.position;
                    Assert.Less((expected - actual).magnitude, 1e-3f,
                        $"Balloon at {slot} settled off its slot (expected {expected}, was {actual}).");
                }
            }
        }
    }
}
