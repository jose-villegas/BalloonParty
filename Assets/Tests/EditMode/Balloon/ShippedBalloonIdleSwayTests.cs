using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     Pins the shipped idle sway to the Animator controller it replaced. The sway <c>BalloonSwayTicker</c> plays
    ///     is a baked copy (<c>BalloonsConfiguration</c>'s Idle Sway block) of the controller's two clips and four
    ///     transitions; nothing compiles that copy against its source, so an edit to a clip or a transition without
    ///     a new bake would leave the balloons playing the old sway. These fail until it's baked again
    ///     (<c>Tools ▸ BalloonParty ▸ Balloons ▸ Bake Idle Sway</c>).
    ///     <para>
    ///         The baked curves are compared with what an Animator plays, not with the clips' drawn curves: the
    ///         two differ where a key's tangent is weighted (<c>BalloonIdleSwayBaker.AsPlayed</c>).
    ///     </para>
    /// </summary>
    [TestFixture]
    public class ShippedBalloonIdleSwayTests
    {
        private const string ConfigPath = "Assets/Configuration/BalloonsConfiguration.asset";
        private const string PrefabFolder = "Assets/Prefabs/Balloon";
        private const string PlainBalloon = "Balloon";
        private const string Rebake = "Run Tools > BalloonParty > Balloons > Bake Idle Sway.";

        // The clips swing through up to 210 degrees a second; a thousandth of a degree is under a hundredth of
        // a pixel at a balloon's edge.
        private const double ToleranceDegrees = 1e-3;

        // Shares no factor with the clips' 60 frames a second, so the steps land all over the frames.
        private const float StepSeconds = 1f / 97f;
        private const int SettleSteps = 40;
        private const int ComparedSteps = 320;

        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        private BalloonsConfiguration _config;
        private IBalloonIdleSwaySettings _sway;
        private AnimatorController _controller;
        private GameObject _rig;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<BalloonsConfiguration>(ConfigPath);
            Assert.IsNotNull(_config, $"{ConfigPath} should load");
            _sway = _config.IdleSway;
            _controller = _sway.Source as AnimatorController;
            Assert.IsNotNull(_controller, "the idle sway has no source controller: it was never baked. " + Rebake);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig != null)
            {
                Object.DestroyImmediate(_rig);
            }
        }

        [Test]
        public void BakedSway_IsWhatItsControllerBakesToNow()
        {
            var fresh = FreshBake();

            Assert.AreEqual(fresh.PivotPath, _sway.PivotPath, "the swayed child moved. " + Rebake);
            AssertSameLoop(fresh.Stable, _sway.Stable, "stable");
            AssertSameLoop(fresh.Unstable, _sway.Unstable, "unstable");
        }

        [Test]
        public void Bake_WritesWhatItReads()
        {
            var blank = ScriptableObject.CreateInstance<BalloonsConfiguration>();
            try
            {
                var problems = new List<string>();
                Assert.IsTrue(BalloonIdleSwayBaker.Bake(blank, _controller, problems), string.Join("\n", problems));

                var fresh = FreshBake();
                Assert.AreSame(_controller, blank.IdleSway.Source);
                Assert.AreEqual(fresh.PivotPath, blank.IdleSway.PivotPath);
                AssertSameLoop(fresh.Stable, blank.IdleSway.Stable, "stable");
                AssertSameLoop(fresh.Unstable, blank.IdleSway.Unstable, "unstable");
            }
            finally
            {
                Object.DestroyImmediate(blank);
            }
        }

        [Test]
        public void StableLoop_ReadsWhatTheAnimatorPlays_AcrossTheLoop()
        {
            AssertReadsWhatTheAnimatorPlays(_sway.Stable, true);
        }

        [Test]
        public void UnstableLoop_ReadsWhatTheAnimatorPlays_AcrossTheLoop()
        {
            AssertReadsWhatTheAnimatorPlays(_sway.Unstable, false);
        }

        [Test]
        public void AsPlayed_LeavesACurveWithNoWeightedKeysAsDrawn()
        {
            var drawn = new AnimationCurve(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 10f, 0f, 0f));
            var clip = new AnimationClip { frameRate = 10f };
            try
            {
                var problems = new List<string>();
                Assert.AreSame(drawn, BalloonIdleSwayBaker.AsPlayed(drawn, clip, problems));
                Assert.IsEmpty(problems);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AsPlayed_TurnsAWeightedStretchIntoStraightLinesBetweenTheClipsFrames_AndLeavesTheRest()
        {
            const float third = 1f / 3f;
            var drawn = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 10f, 0f, 0f, third, third) { weightedMode = WeightedMode.Both },
                new Keyframe(2f, 0f, 0f, 0f),
                new Keyframe(3f, 10f, 0f, 0f));
            var clip = new AnimationClip { frameRate = 10f };
            try
            {
                var problems = new List<string>();
                var played = BalloonIdleSwayBaker.AsPlayed(drawn, clip, problems);

                Assert.IsEmpty(problems);
                Assert.AreEqual(22, played.length, "a key a frame over the two weighted seconds, then the two drawn");

                // On a frame the played curve is the drawn one; between two it is the straight line across.
                Assert.AreEqual(drawn.Evaluate(0.3f), played.Evaluate(0.3f), 1e-5f);
                Assert.AreEqual(drawn.Evaluate(1.7f), played.Evaluate(1.7f), 1e-5f);
                var across = (drawn.Evaluate(0.3f) + drawn.Evaluate(0.4f)) * 0.5f;
                Assert.AreEqual(across, played.Evaluate(0.35f), 1e-4f);
                Assert.Greater(Mathf.Abs(drawn.Evaluate(0.35f) - across), 1e-2f, "the drawn curve bows off that line");
                across = (drawn.Evaluate(1.9f) + drawn.Evaluate(2f)) * 0.5f;
                Assert.AreEqual(across, played.Evaluate(1.95f), 1e-4f, "the last frame of a weighted stretch too");

                // Past the weighted keys the curve is as drawn.
                Assert.AreEqual(drawn.Evaluate(2.05f), played.Evaluate(2.05f), 1e-5f);
                Assert.AreEqual(drawn.Evaluate(2.55f), played.Evaluate(2.55f), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        // The ticker writes the child the clips name; the view resets the one it calls its sway pivot. They
        // have to be one transform, level in the prefab.
        [Test]
        public void SwayedPrefabs_SwayTheChildTheirAnimatorTurned()
        {
            var swayed = new List<string>();
            foreach (var view in BalloonPrefabs())
            {
                var animator = AnimatorOf(view);
                if (animator == null || animator.runtimeAnimatorController != _sway.Source)
                {
                    continue;
                }

                swayed.Add(view.name);
                var target = animator.transform.Find(_sway.PivotPath);
                Assert.IsNotNull(target, $"{view.name} has no '{_sway.PivotPath}' under its Animator");
                Assert.AreSame(view.RotationPivot, target, $"{view.name}'s sway pivot isn't the child its clips turn");
                Assert.Less(Quaternion.Angle(Quaternion.identity, target.localRotation), 1e-4f,
                    $"{view.name}'s sway pivot isn't level in the prefab");
            }

            CollectionAssert.Contains(swayed, PlainBalloon, "the plain balloon no longer plays the baked controller");
        }

        // Tough, unbreakable and the plated reward balloons animate more than a sway (scale, a second pivot,
        // material properties, a one-shot), which the ticker doesn't play. One whose controller ever becomes a
        // plain sway would be paying for an Animator it doesn't need.
        [Test]
        public void PrefabsThatKeepTheirAnimator_PlayMoreThanASway()
        {
            foreach (var view in BalloonPrefabs())
            {
                var animator = AnimatorOf(view);
                var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
                if (controller == null || controller == _sway.Source)
                {
                    continue;
                }

                Assert.IsNull(BalloonIdleSwayBaker.Read(controller, new List<string>()),
                    $"{view.name} plays '{controller.name}', which is only a sway now: the ticker could play it");
            }
        }

        private BalloonIdleSwaySettings FreshBake()
        {
            var problems = new List<string>();
            var fresh = BalloonIdleSwayBaker.Read(_controller, problems);
            Assert.IsNotNull(fresh,
                $"'{_controller.name}' does something the sway ticker doesn't play:\n" + string.Join("\n", problems));
            return fresh;
        }

        // Plays the loop's state on a real Animator, stepped by hand, and reads the baked curve at the
        // Animator's own time in the state.
        private void AssertReadsWhatTheAnimatorPlays(IBalloonSwayLoop loop, bool stable)
        {
            var pivot = BuildRig(_sway.PivotPath);
            var animator = _rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = _controller;
            _rig.SetActive(false);
            _rig.SetActive(true);
            if (!animator.isInitialized)
            {
                animator.Rebind();
            }

            animator.SetBool(IsStableParam, stable);
            for (var i = 0; i < SettleSteps; i++)
            {
                animator.Update(StepSeconds);
            }

            var worst = 0.0;
            var worstAt = 0f;
            var widest = 0.0;
            for (var i = 0; i < ComparedSteps; i++)
            {
                animator.Update(StepSeconds);
                Assert.IsFalse(animator.IsInTransition(0), "the Animator should have settled into the loop's state");

                var rotation = pivot.localRotation;
                Assert.AreEqual(0f, rotation.x, 1e-6f, "the Animator turns the pivot about X");
                Assert.AreEqual(0f, rotation.y, 1e-6f, "the Animator turns the pivot about Y");

                var played = 2.0 * Math.Atan2(rotation.z, rotation.w) * Mathf.Rad2Deg;
                var seconds = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) * loop.Seconds;
                var error = Math.Abs(played - loop.AngleAt(seconds));
                widest = Math.Max(widest, Math.Abs(played));
                if (error > worst)
                {
                    worst = error;
                    worstAt = seconds;
                }
            }

            if (widest < 1.0)
            {
                Assert.Inconclusive("The Animator didn't play when stepped by hand outside Play mode.");
            }

            Assert.LessOrEqual(worst, ToleranceDegrees,
                $"the Animator and the baked {(stable ? "stable" : "unstable")} curve part by {worst:F5} degrees " +
                $"{worstAt:F4} s into the loop. " + Rebake);
        }

        // A stand-in for a balloon: the swayed child under a root, at the path the clips name.
        private Transform BuildRig(string pivotPath)
        {
            _rig = new GameObject("SwayRig");
            var parent = _rig.transform;
            foreach (var name in pivotPath.Split('/'))
            {
                var child = new GameObject(name).transform;
                child.SetParent(parent, false);
                parent = child;
            }

            return parent;
        }

        // By what the curves read, not key for key: a bake in the Editor and the shipped asset may round a key
        // differently and still be the same curve.
        private static void AssertSameLoop(IBalloonSwayLoop expected, IBalloonSwayLoop baked, string name)
        {
            Assert.AreEqual(expected.Seconds, baked.Seconds, $"the {name} loop's length changed. " + Rebake);
            Assert.AreEqual(expected.EnterSeconds, baked.EnterSeconds, $"the enter time of {name} changed. " + Rebake);
            Assert.AreEqual(expected.SwitchSeconds, baked.SwitchSeconds, $"the crossfade to {name} changed. " + Rebake);
            Assert.AreEqual(((BalloonSwayLoop)expected).Angle.length, ((BalloonSwayLoop)baked).Angle.length,
                $"the {name} clip's keys changed. " + Rebake);

            const int samples = 3000;
            for (var i = 0; i <= samples; i++)
            {
                var seconds = expected.Seconds * i / samples;
                Assert.AreEqual(expected.AngleAt(seconds), baked.AngleAt(seconds), 1e-4f,
                    $"the {name} clip's curve changed at {seconds} s. " + Rebake);
            }
        }

        private static IEnumerable<BalloonView> BalloonPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            Assert.IsNotEmpty(guids, $"no prefabs found under {PrefabFolder}");
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.TryGetComponent<BalloonView>(out var view))
                {
                    yield return view;
                }
            }
        }

        private static Animator AnimatorOf(BalloonView view)
        {
            var field = typeof(BalloonView).GetField("_animator", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "BalloonView._animator was renamed; update this test");
            return (Animator)field.GetValue(view);
        }
    }
}
