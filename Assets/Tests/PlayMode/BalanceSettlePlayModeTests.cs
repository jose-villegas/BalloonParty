using System.Collections;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives the frame-deferred balancer (scan + DOTween move across frames) that EditMode can't
    ///     run: spawns a wave, then asserts the board reaches a settled state — no balance move left in
    ///     transit — within the timeout, and stays settled the next frame.
    /// </summary>
    public class BalanceSettlePlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator SpawnWave_BoardSettles()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var linePublisher = Resolve<IPublisher<SpawnBalloonLineMessage>>();
            var transit = Resolve<BalancePathHolder>();
            var pause = Resolve<PauseService>();

            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned.");

            for (var i = 0; i < 3; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            yield return WaitUntil(() => !pause.IsAnyPaused.Value, message: "Overflow hold never released.");
            yield return WaitUntil(() => !AnyInTransit(transit, grid),
                message: "Board never settled — a balance move stayed in transit.");

            yield return null;
            Assert.IsFalse(AnyInTransit(transit, grid), "Board re-entered transit after settling.");
        }
    }
}
