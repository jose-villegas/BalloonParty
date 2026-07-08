using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Item;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Drives a laser activation against real balloon colliders — the Physics2D.CircleCast cross
    ///     EditMode can't exercise. Fills the board, feeds the handler an axis-aligned captured rotation,
    ///     activates on an interior balloon, and asserts the cross destroys at least one balloon.
    /// </summary>
    public class LaserActivationPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator Activate_PopsBalloonsAlongCross()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var laser = Resolve<IEnumerable<IBalloonItem>>().First(handler => handler.Type == ItemType.Laser);
            var rotationPublisher = Resolve<IPublisher<TransformCapturedMessage>>();

            yield return FillAndSettle(grid);

            if (!TryFindInteriorBalloon(grid, out var slot, out var model))
            {
                Assert.Inconclusive("No interior balloon with a populated neighbourhood to fire at.");
                yield break;
            }

            // The cross is physics (CircleCast) — wait for the target's collider to actually arrive.
            yield return WaitForColliderAt(grid, slot);

            // The handler reads a per-balloon rotation captured via TransformCapturedMessage; without one
            // the cross casts along a zero quaternion. Feed an identity rotation so it casts world axes.
            var probe = new GameObject("LaserRotationProbe").transform;
            rotationPublisher.Publish(new TransformCapturedMessage((ISlotActor)model, new TransformSnapshot(probe)));

            var before = BalloonCount(grid);

            // Activate is synchronous (returns CompletedTask) — the cross dispatches its hits inline.
            _ = laser.Activate(new ItemActivationContext(model, grid.IndexToWorldPosition(slot), Vector3.zero));

            yield return WaitUntil(() => BalloonCount(grid) < before, timeout: 5f,
                message: "Laser cross did not remove any balloon.");

            Object.Destroy(probe.gameObject);
        }
    }
}
