using System.Collections;
using BalloonParty.Game.Health;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives the spawn-saturation loss loop in the real Game scene — the pressure-balance /
    ///     reject / HP behaviour EditMode can't exercise. The key property: a flood of spawn lines
    ///     packs the board (re-home + pressure fill every reachable and pushable slot) before any
    ///     hit point is lost, and once there is genuinely no room HP drains to zero and the run ends.
    /// </summary>
    public class PressureLossPlayModeTests : PlayModeGameTest
    {
        private const float LoopTimeout = 15f;
        private const int MaxLines = 60;
        private const int FramesPerLine = 4;

        // This fixture exists to drain HP to zero — the run-loss lock would make that impossible.
        protected override bool ProtectRunFromLoss => false;

        [UnityTest]
        public IEnumerator InitialLoad_HealthStartsAtConfiguredHitPoints()
        {
            yield return LoadGameScene();

            var health = Resolve<PlayerHealthController>();
            var config = Resolve<IRunConfig>();

            Assert.AreEqual(config.StartingHitPoints, health.Current.Value,
                "Player HP should initialise to the configured starting value.");
        }

        [UnityTest]
        public IEnumerator Saturation_FillsBoardThenDrainsHpToGameOver()
        {
            yield return LoadGameScene();

            var scope = Scope();
            var grid = scope.Container.Resolve<SlotGrid>();
            var health = scope.Container.Resolve<PlayerHealthController>();
            var navigation = scope.Container.Resolve<INavigation>();
            var linePublisher = scope.Container.Resolve<IPublisher<SpawnBalloonLineMessage>>();

            yield return WaitUntil(() => BalloonCount(grid) > 0);

            var startingHp = health.Current.Value;
            Assert.Greater(startingHp, 0, "Run should start with hit points.");

            var totalSlots = grid.Columns * grid.Rows;
            var fillAtFirstHpLoss = -1;
            var elapsed = 0f;
            var frame = 0;
            var linesPublished = 0;

            // Flood the board with spawn lines and watch HP. Lines are paced a few frames apart so the
            // staggered reject pops (which publish the HP-draining SpawnBlockedMessage) can resolve.
            while (health.Current.Value > 0 && elapsed < LoopTimeout)
            {
                if (linesPublished < MaxLines && frame % FramesPerLine == 0)
                {
                    linePublisher.Publish(new SpawnBalloonLineMessage(1));
                    linesPublished++;
                }

                if (fillAtFirstHpLoss < 0 && health.Current.Value < startingHp)
                {
                    fillAtFirstHpLoss = BalloonCount(grid);
                }

                elapsed += Time.unscaledDeltaTime;
                frame++;
                yield return null;
            }

            Assert.AreEqual(0, health.Current.Value, "Saturating the board should drain HP to zero.");
            Assert.GreaterOrEqual(fillAtFirstHpLoss, totalSlots / 2,
                "The board should fill well past half (pressure balance using every reachable/pushable " +
                "slot) before any hit point is lost.");
            Assert.AreEqual(NavigationState.GameOver, navigation.Current.Value,
                "Reaching zero HP should end the run.");
        }
    }
}
