using System.Collections;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Runs several restart cycles to exercise pool Get/Return across recycles. The manager doesn't
    ///     expose active/idle counts, so this asserts the achievable integration invariant: every cycle
    ///     repopulates the board and no error/exception log is emitted — a leak, double-return, or
    ///     dangling reference would surface as one or the other.
    /// </summary>
    public class PoolIntegrityPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator RestartCycles_RepopulateWithoutErrors()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var runController = Resolve<RunController>();

            yield return WaitUntil(() => BalloonCount(grid) > 0, message: "Balloons never spawned on load.");

            for (var cycle = 0; cycle < 3; cycle++)
            {
                runController.EndRun();
                yield return null;

                runController.RestartRun();
                yield return WaitUntil(() => BalloonCount(grid) > 0,
                    message: $"Board did not repopulate after restart cycle {cycle}.");
            }

            // Errors/exceptions still fail the test by default — a pool leak, double-return, or dangling
            // reference surfaces as one. (A benign DOTween teardown-timing warning under rapid restart is
            // not gated here.)
        }
    }
}
