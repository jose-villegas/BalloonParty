using System.Collections;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
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
    ///     Fires the loaded projectile at a balloon and asserts the full flight → collision → pop →
    ///     score path — the real collider travel + OnTriggerEnter2D that EditMode can't drive. Firing is
    ///     ThrowerView-input-gated, so it captures the loaded model from `ProjectileLoadedMessage` and
    ///     launches it directly (`Direction`/`IsFree`, exactly what the thrower's fire does).
    /// </summary>
    public class ShotLoopPlayModeTests : PlayModeGameTest
    {
        [UnityTest]
        public IEnumerator FiredProjectile_PopsBalloonAndScores()
        {
            yield return LoadGameScene();

            var grid = Resolve<SlotGrid>();
            var positions = Resolve<ProjectilePositionProvider>();

            // The thrower loads a projectile after its entrance animation (post scene-load), so this
            // subscription catches that load; the message carries the active model.
            IWriteableProjectileModel projectile = null;
            var loadedSub = Resolve<ISubscriber<ProjectileLoadedMessage>>()
                .Subscribe(msg => projectile = (IWriteableProjectileModel)msg.Model);
            yield return WaitUntil(() => projectile != null, message: "Thrower never loaded a projectile.");

            yield return FillAndSettle(grid);

            if (!TryFindInteriorBalloon(grid, out var slot, out _))
            {
                Assert.Inconclusive("No settled balloon to shoot at.");
                yield break;
            }

            yield return WaitForColliderAt(grid, slot);

            var scored = false;
            var scoreSub = Resolve<ISubscriber<ScorePointMessage>>().Subscribe(_ => scored = true);
            var before = BalloonCount(grid);

            // Launch straight at the target — the projectile view moves on IsFree; once free the thrower's
            // Tick leaves it alone (TryFire / reposition both early-return on IsFree).
            projectile.Direction = ((Vector2)(grid.IndexToWorldPosition(slot) - positions.Position)).normalized;
            projectile.IsFree = true;

            yield return WaitUntil(() => BalloonCount(grid) < before,
                message: "Fired projectile never popped a balloon.");
            yield return WaitUntil(() => scored, timeout: 3f, message: "The pop did not credit a score point.");

            loadedSub.Dispose();
            scoreSub.Dispose();
        }
    }
}
