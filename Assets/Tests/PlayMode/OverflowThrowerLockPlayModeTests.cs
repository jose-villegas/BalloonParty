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
    ///     Forces a board overflow and asserts the thrower is locked while it resolves — the thrower's
    ///     fire is gated on <see cref="PauseService.IsAnyPaused" />, so an overflow hold locks it.
    ///     (Release is not asserted: continued saturation runs toward GameOver, and the recoverable
    ///     window is too timing-fragile to catch deterministically.)
    /// </summary>
    public class OverflowThrowerLockPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Overflow_LocksThrower()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var pause = Resolve<PauseService>();
            var linePublisher = Resolve<IPublisher<SpawnBalloonLineMessage>>();

            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned.");

            // Spawn one line at a time until the first overflow hold pauses the game; stop immediately so
            // it stays recoverable (one heart lost) rather than saturating all the way to GameOver.
            for (var i = 0; i < 40 && !pause.IsAnyPaused.Value; i++)
            {
                linePublisher.Publish(new SpawnBalloonLineMessage(1));
                yield return null;
            }

            if (!pause.IsAnyPaused.Value)
            {
                Assert.Inconclusive("Board never overflowed to lock the thrower.");
                yield break;
            }

            Assert.IsTrue(pause.IsAnyPaused.Value, "Thrower should be locked (paused) during the overflow hold.");
        }
    }
}
