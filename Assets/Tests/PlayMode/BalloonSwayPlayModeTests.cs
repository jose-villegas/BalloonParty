using System.Collections;
using System.Collections.Generic;
using System.Text;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Cheats;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     The idle sway's wiring on real balloons in the Game scene: which ones the ticker sways and which keep
    ///     their Animator, and what <see cref="BalloonView" /> tells the ticker as it spawns, is reused and has
    ///     its sway stopped. That the ticker's angles are the Animator's own is
    ///     <see cref="BalloonSwayAnimatorParityPlayModeTests" />' to prove; here a reused view's first two frames
    ///     are checked against a fresh playback.
    ///     <para>
    ///         None of these waits for the whole board to settle: lines keep arriving on the spawn clock and a
    ///         formation keeps walking, so there may never be a frame on which every balloon is stable. Each
    ///         takes one plain balloon that has landed in its slot on the grid, which is all it needs. (Not one
    ///         from the overflow pile: those leave the scene on their own.)
    ///     </para>
    /// </summary>
    public class BalloonSwayPlayModeTests : PlayModeGameTest
    {
        private const float ToleranceDegrees = 1e-3f;
        private const int WatchedFrames = 20;

        [TearDown]
        public void ClearPerfSwitch()
        {
            CheatState.PerfOff = PerfOff.None;
        }

        [UnityTest]
        public IEnumerator PlainBalloons_SwayWithTheirAnimatorOff_TheOthersKeepTheirs()
        {
            yield return LoadGameScene();
            var sway = Resolve<IBalloonsConfiguration>().IdleSway;
            yield return WaitForLandedPlainBalloon(sway);

            var watched = new List<BalloonView>();
            var models = new List<IBalloonModel>();
            var starts = new List<Quaternion>();
            foreach (var view in LiveBalloons())
            {
                if (!view.TryGetComponent<Animator>(out var animator))
                {
                    continue;
                }

                if (animator.runtimeAnimatorController != sway.Source)
                {
                    Assert.IsTrue(animator.enabled, $"{view.name} animates more than a sway and keeps its Animator");
                    continue;
                }

                Assert.IsFalse(animator.enabled, $"{view.name} is swayed by the ticker; its Animator should be off");
                watched.Add(view);
                models.Add(view.Model);
                starts.Add(view.RotationPivot.localRotation);
            }

            var moved = new float[watched.Count];
            for (var frame = 0; frame < WatchedFrames; frame++)
            {
                yield return null;
                for (var i = 0; i < watched.Count; i++)
                {
                    // One that left the board while being watched (popped, reused) proves nothing either way.
                    if (watched[i] != null && ReferenceEquals(watched[i].Model, models[i]))
                    {
                        moved[i] = Mathf.Max(moved[i],
                            Quaternion.Angle(starts[i], watched[i].RotationPivot.localRotation));
                    }
                    else
                    {
                        models[i] = null;
                    }
                }
            }

            var stayed = 0;
            for (var i = 0; i < watched.Count; i++)
            {
                if (models[i] == null)
                {
                    continue;
                }

                stayed++;
                Assert.Greater(moved[i], 0.05f, $"{watched[i].name} isn't swaying");
            }

            Assert.Greater(stayed, 0, $"none of the {watched.Count} plain balloons stayed on the board to be watched");
        }

        [UnityTest]
        public IEnumerator ReusedBalloon_StartsItsSwayOverLevel()
        {
            yield return LoadGameScene();
            var sway = Resolve<IBalloonsConfiguration>().IdleSway;

            // Tilted, so that starting level is a change; landed, so that no flight ends and flips it to the
            // stable loop in the two frames watched.
            BalloonView view = null;
            yield return WaitFor(sway, "a landed plain balloon tilted more than 2 degrees", () =>
            {
                view = LandedPlainBalloon(sway, 2f);
                return view != null;
            });
            var pivot = view.RotationPivot;

            // The pool reusing it. A frame's delta time is what the ticker's pass later that frame steps by.
            var playback = default(BalloonSwayPlayback);
            var deltaTime = Time.deltaTime;
            view.OnSpawned();
            yield return null;

            Assert.AreEqual(playback.Advance(deltaTime, sway), Tilt(pivot), ToleranceDegrees,
                "a reused balloon should start level, whatever tilt it was left at");

            deltaTime = Time.deltaTime;
            yield return null;

            Assert.AreEqual(playback.Advance(deltaTime, sway), Tilt(pivot), ToleranceDegrees,
                "and play the unstable loop from its start");
            Assert.Greater(Mathf.Abs(Tilt(pivot)), 0.05f, "the loop should have moved it off level");
        }

        [UnityTest]
        public IEnumerator StoppedSway_HoldsItsPose_AndStartsLevelWhenTheBalloonIsReused()
        {
            yield return LoadGameScene();
            var sway = Resolve<IBalloonsConfiguration>().IdleSway;
            yield return WaitForLandedPlainBalloon(sway);
            var view = LandedPlainBalloon(sway, 0f);
            var pivot = view.RotationPivot;

            view.SuspendAnimator();
            var held = pivot.localRotation;
            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.AreEqual(held, pivot.localRotation, "a stopped sway must leave the pivot to the cinematic");
            }

            // A float-away's tilt, left on the pivot when the balloon goes back to the pool.
            pivot.localRotation = Quaternion.Euler(0f, 0f, 25f);
            var playback = default(BalloonSwayPlayback);
            var deltaTime = Time.deltaTime;
            view.OnSpawned();
            yield return null;

            Assert.AreEqual(playback.Advance(deltaTime, sway), Tilt(pivot), ToleranceDegrees,
                "a balloon whose sway was stopped starts level, not from the cinematic's tilt");

            deltaTime = Time.deltaTime;
            yield return null;

            Assert.AreEqual(playback.Advance(deltaTime, sway), Tilt(pivot), ToleranceDegrees,
                "and plays the unstable loop from its start on the next frame");
        }

        [UnityTest]
        public IEnumerator PerfSwitch_LeavesASpawnedBalloonWithoutASway()
        {
            yield return LoadGameScene();
            var sway = Resolve<IBalloonsConfiguration>().IdleSway;
            yield return WaitForLandedPlainBalloon(sway);
            var view = LandedPlainBalloon(sway, 0f);
            var pivot = view.RotationPivot;

            CheatState.PerfOff = PerfOff.Animators;
            view.OnSpawned();

            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.Less(Quaternion.Angle(Quaternion.identity, pivot.localRotation), 1e-4f,
                    "with the animators switched off for a measurement, nothing sways the balloon");
            }

            Assert.IsFalse(view.GetComponent<Animator>().enabled);
        }

        private static IEnumerator WaitForLandedPlainBalloon(IBalloonIdleSwaySettings sway)
        {
            return WaitFor(sway, "a plain balloon landed in its slot", () => LandedPlainBalloon(sway, 0f) != null);
        }

        // WaitUntil with the board's state in the failure, so a timeout says what was there instead of what
        // wasn't.
        private static IEnumerator WaitFor(IBalloonIdleSwaySettings sway, string what, System.Func<bool> found)
        {
            var elapsed = 0f;
            while (!found() && elapsed < DefaultTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!found())
            {
                Assert.Fail($"No {what} after {elapsed:F1} s. {DescribeBoard(sway)}");
            }
        }

        private static string DescribeBoard(IBalloonIdleSwaySettings sway)
        {
            var grid = Resolve<SlotGrid>();
            var text = new StringBuilder();
            var live = 0;
            foreach (var view in LiveBalloons())
            {
                live++;
                var plain = view.TryGetComponent<Animator>(out var animator)
                            && animator.runtimeAnimatorController == sway.Source;
                text.Append(
                    $"\n  {view.name}: {(plain ? "plain" : "other")}, on the grid {OnTheGrid(grid, view)}, " +
                    $"stable {view.Model.IsStable.Value}, tilt {Tilt(view.RotationPivot):F2}, " +
                    $"scale {view.transform.localScale.x:F2}, " +
                    $"Animator {(animator != null ? animator.enabled.ToString() : "none")}");
            }

            return $"Time scale {Time.timeScale}, frame {Time.frameCount}, {live} live balloons:{text}";
        }

        private static IEnumerable<BalloonView> LiveBalloons()
        {
            foreach (var view in Object.FindObjectsByType<BalloonView>())
            {
                if (view.Model != null)
                {
                    yield return view;
                }
            }
        }

        // A plain balloon (its Animator plays the baked controller) in a slot of the grid, whose flight there
        // has ended, tilted at least `minimumTilt` degrees. Null if there is none this frame.
        private static BalloonView LandedPlainBalloon(IBalloonIdleSwaySettings sway, float minimumTilt)
        {
            var grid = Resolve<SlotGrid>();
            foreach (var view in LiveBalloons())
            {
                if (view.Model.IsStable.Value
                    && OnTheGrid(grid, view)
                    && view.TryGetComponent<Animator>(out var animator)
                    && animator.runtimeAnimatorController == sway.Source
                    && Mathf.Abs(Tilt(view.RotationPivot)) >= minimumTilt)
                {
                    return view;
                }
            }

            return null;
        }

        private static bool OnTheGrid(SlotGrid grid, BalloonView view)
        {
            var slot = view.Model.SlotIndex.Value;
            return grid.InBounds(slot) && ReferenceEquals(grid.At(slot), view.Model);
        }

        private static float Tilt(Transform pivot)
        {
            return Mathf.DeltaAngle(0f, pivot.localEulerAngles.z);
        }
    }
}
