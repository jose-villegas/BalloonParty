using System.Collections.Generic;
using System.Text;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Balloons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     Steps a real Animator on the baked controller by hand (<c>Animator.Update</c>) beside a
    ///     <see cref="BalloonSwayPlayback" /> given the same steps, and compares the angle each gives the pivot at
    ///     every step: through a spawn, <c>IsStable</c> flipping while settled and mid-crossfade, a loop's wrap, and
    ///     a pooled reuse. This is the check that the playback's rules are the Animator's.
    ///     <para>
    ///         A mismatch doesn't stop at the first step apart. The report lists the steps after every spawn and
    ///         flip, the steps around each start and end of an Animator transition, and the steps apart, each with
    ///         the Animator's own account of its states, so one run shows every rule the playback has wrong.
    ///     </para>
    ///     <para>
    ///         The PlayMode <c>BalloonSwayAnimatorParityPlayModeTests</c> runs the same comparison in the player
    ///         loop, where the game runs the Animator. Where an Animator stepped by hand outside Play mode stays
    ///         inert these report inconclusive rather than fail.
    ///     </para>
    /// </summary>
    [TestFixture]
    public class BalloonSwayAnimatorParityTests
    {
        private const string ConfigPath = "Assets/Configuration/BalloonsConfiguration.asset";
        private const string PlainBalloonPath = "Assets/Prefabs/Balloon/Balloon.prefab";
        private const string PlayModeTwin = "BalloonSwayAnimatorParityPlayModeTests is the same check in Play mode.";

        // Not a divisor of the 0.25 s and 0.75 s crossfades: a crossfade that ended exactly on a step would end
        // a step sooner or later on float rounding alone.
        private const float StepSeconds = 1f / 70f;

        // A step's slip in the start of a loop or a crossfade is a tenth of a degree at the least, a straight
        // line where the Animator mixes rotations up to four hundredths, a drawn curve where it plays straight
        // lines up to one. The playback counts seconds where the Animator counts loops, so the two round apart
        // by well under a thousandth.
        private const float ToleranceDegrees = 0.005f;

        private static readonly int IsStableParam = Animator.StringToHash("IsStable");

        private IBalloonIdleSwaySettings _sway;
        private GameObject _balloon;
        private Transform _pivot;
        private Animator _animator;
        private BalloonSwayPlayback _playback;
        private SwayParityReport _report;
        private float _widestAngle;

        [SetUp]
        public void SetUp()
        {
            var config = AssetDatabase.LoadAssetAtPath<BalloonsConfiguration>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} should load");
            _sway = config.IdleSway;
            Assert.IsNotNull(_sway.Source, "the idle sway was never baked");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlainBalloonPath);
            Assert.IsNotNull(prefab, $"{PlainBalloonPath} should load");
            var authored = prefab.GetComponent<Animator>();
            Assert.AreSame(_sway.Source, authored.runtimeAnimatorController,
                "the plain balloon no longer plays the baked controller");

            _widestAngle = 0f;
            BuildBalloon(authored);
            _report = new SwayParityReport(_animator, _sway, ToleranceDegrees);
        }

        [TearDown]
        public void TearDown()
        {
            if (_balloon != null)
            {
                Object.DestroyImmediate(_balloon);
            }
        }

        [Test]
        public void Playback_GivesTheAnimatorsAngle_StepForStep()
        {
            // As BalloonFactory spawns one: unstable for its flight in, set before the first evaluation.
            Spawn(false);
            Step(7);

            // 0.1 s into the 0.25 s enter time: the flip has to wait for it.
            SetStable(true);
            Step(98);

            SetStable(false);
            Step(21);

            // 0.3 s into the 0.75 s crossfade: this one has to wait as well, then crossfade back.
            SetStable(true);
            Step(175);

            // Long enough on the wide swing to pass the loop's wrap.
            SetStable(false);
            Step(300);

            AssertMatched();
        }

        [Test]
        public void ReusedBalloon_StartsOverLevel_WhateverTiltItWasLeftAt_AsTheAnimatorDid()
        {
            Spawn(false);
            Step(40);
            Assert.Greater(Mathf.Abs(Tilt()), 5f, "the balloon should be mid-swing when it's despawned");

            _balloon.SetActive(false);
            Spawn(true);
            Step(100);

            AssertMatched();
        }

        [Test]
        public void BalloonReusedAfterItsSwayWasStopped_StartsLevel_AsTheAnimatorDid()
        {
            Spawn(true);
            Step(40);

            // A float-away: the sway stops and the cinematic tilts the pivot itself.
            _animator.enabled = false;
            _pivot.localRotation = Quaternion.Euler(0f, 0f, 25f);

            _balloon.SetActive(false);
            Spawn(false);
            Step(100);

            AssertMatched();
        }

        // The swayed child under a root with an Animator set up as the prefab's is, waiting inactive like a
        // pooled balloon.
        private void BuildBalloon(Animator authored)
        {
            _balloon = new GameObject("SwayParityBalloon");
            _pivot = _balloon.transform;
            foreach (var name in _sway.PivotPath.Split('/'))
            {
                var child = new GameObject(name).transform;
                child.SetParent(_pivot, false);
                _pivot = child;
            }

            _animator = _balloon.AddComponent<Animator>();
            _animator.runtimeAnimatorController = _sway.Source;
            _animator.updateMode = authored.updateMode;
            _animator.cullingMode = authored.cullingMode;
            _animator.applyRootMotion = authored.applyRootMotion;
            _animator.keepAnimatorStateOnDisable = authored.keepAnimatorStateOnDisable;
            _animator.writeDefaultValuesOnDisable = authored.writeDefaultValuesOnDisable;
            _animator.speed = authored.speed;
            _balloon.SetActive(false);
        }

        // The pool's Get and BalloonView.OnSpawned, in their order: activate (the Animator binds here, with
        // whatever tilt the last life left on the pivot), reset the pivot, start the sway, bind the model's
        // IsStable.
        private void Spawn(bool stable)
        {
            _balloon.SetActive(true);
            var left = Tilt();
            _pivot.localRotation = Quaternion.identity;
            _animator.enabled = true;
            if (!_animator.isInitialized)
            {
                _animator.Rebind();
            }

            _animator.SetBool(IsStableParam, stable);
            _playback = default;
            _playback.Stable = stable;
            _report.Note($"spawned: IsStable {stable}, the pivot had been left at {left:F4}");
        }

        private void SetStable(bool stable)
        {
            _animator.SetBool(IsStableParam, stable);
            _playback.Stable = stable;
            _report.Note($"IsStable set {stable}");
        }

        private void Step(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _animator.Update(StepSeconds);
                var expected = _playback.Advance(StepSeconds, _sway);
                var actual = Tilt();
                _widestAngle = Mathf.Max(_widestAngle, Mathf.Abs(actual));
                _report.Frame(StepSeconds, expected, actual, _playback);
            }
        }

        private void AssertMatched()
        {
            if (_widestAngle < 1f)
            {
                Assert.Inconclusive(
                    "The Animator didn't play when stepped by hand outside Play mode, so there was nothing to " +
                    "compare. " + PlayModeTwin);
            }

            Assert.IsTrue(_report.Matched, _report.Text() + "\n" + PlayModeTwin);
        }

        private float Tilt()
        {
            return Mathf.DeltaAngle(0f, _pivot.localEulerAngles.z);
        }

        /// <summary>
        ///     Every compared step, and which of them are worth reading when some are apart: those after a spawn or
        ///     a flip, those around a change in the Animator's state or transition, and those apart.
        /// </summary>
        private sealed class SwayParityReport
        {
            private const int ShownAfterNote = 6;
            private const int ShownAfterChange = 3;
            private const int ShownApart = 60;
            private const string Empty = "Empty";
            private const string StableIdle = "StableIdle";
            private const string UnstableIdle = "UnstableIdle";

            private readonly List<string> _lines = new();
            private readonly List<bool> _shown = new();
            private readonly Animator _animator;
            private readonly IBalloonIdleSwaySettings _sway;
            private readonly float _tolerance;

            private int _frames;
            private int _apart;
            private int _showNext;
            private float _worst;
            private int _worstFrame;
            private string _lastAnimatorState;

            internal bool Matched => _apart == 0;

            internal SwayParityReport(Animator animator, IBalloonIdleSwaySettings sway, float tolerance)
            {
                _animator = animator;
                _sway = sway;
                _tolerance = tolerance;
            }

            internal void Note(string text)
            {
                _lines.Add("-- " + text);
                _shown.Add(true);
                _showNext = ShownAfterNote;
            }

            internal void Frame(float deltaTime, float expected, float actual, BalloonSwayPlayback playback)
            {
                _frames++;
                var error = Mathf.Abs(Mathf.DeltaAngle(expected, actual));
                var apart = error > _tolerance;
                if (apart)
                {
                    _apart++;
                }

                if (error > _worst)
                {
                    _worst = error;
                    _worstFrame = _frames;
                }

                var animatorState = AnimatorState(out var animatorText);
                var changed = _lastAnimatorState != null && animatorState != _lastAnimatorState;
                _lastAnimatorState = animatorState;
                if (changed)
                {
                    _shown[^1] = true;
                    _showNext = Mathf.Max(_showNext, ShownAfterChange);
                }

                var playbackText = playback.Blending
                    ? $"crossfade {playback.Blend:F4}, loops at {playback.CurrentSeconds:F4} s -> " +
                      $"{playback.NextSeconds:F4} s"
                    : $"loop at {playback.CurrentSeconds:F4} s";

                _lines.Add(
                    $"{(apart ? "!!" : "  ")} {_frames} (dt {deltaTime:F5}): playback {expected:F4} " +
                    $"({playbackText}); animator {actual:F4} ({animatorText})");
                _shown.Add(_showNext > 0 || (apart && _apart <= ShownApart));
                _showNext = Mathf.Max(0, _showNext - 1);
            }

            internal string Text()
            {
                var text = new StringBuilder();
                text.AppendLine(
                    $"{_apart} of {_frames} steps more than {_tolerance} degrees apart, the worst {_worst:F4} at " +
                    $"step {_worstFrame}. Listed: the {ShownAfterNote} steps after each spawn and flip, the steps " +
                    $"around each change in the Animator's state or transition, and the first {ShownApart} apart " +
                    "(!!). State times are in loops; 'own times read' is the angle the baked curves give at the " +
                    "Animator's own state times and transition progress, mixed as the playback mixes two poses.");

                var skipped = 0;
                for (var i = 0; i < _lines.Count; i++)
                {
                    if (!_shown[i])
                    {
                        skipped++;
                        continue;
                    }

                    if (skipped > 0)
                    {
                        text.AppendLine($"   ... {skipped} steps");
                        skipped = 0;
                    }

                    text.AppendLine(_lines[i]);
                }

                return text.ToString();
            }

            // A key that changes when the Animator's state or transition does, and the text for the line.
            private string AnimatorState(out string text)
            {
                var current = _animator.GetCurrentAnimatorStateInfo(0);
                var currentName = Name(current);
                if (!_animator.IsInTransition(0))
                {
                    text = $"{currentName} at {current.normalizedTime:F4}; own times read " +
                           $"{Describe(Angle(currentName, current))}";
                    return currentName;
                }

                var next = _animator.GetNextAnimatorStateInfo(0);
                var nextName = Name(next);
                var progress = _animator.GetAnimatorTransitionInfo(0).normalizedTime;
                var from = Angle(currentName, current);
                var to = Angle(nextName, next);
                var mixed = from.HasValue && to.HasValue ? BalloonSwayPlayback.Mix(from.Value, to.Value, progress) : to;

                text = $"{currentName} at {current.normalizedTime:F4} -> {nextName} at {next.normalizedTime:F4}, " +
                       $"transition {progress:F4}; own times read {Describe(mixed)}" +
                       (from.HasValue ? string.Empty : " (the entered loop alone)");
                return currentName + ">" + nextName;
            }

            private float? Angle(string stateName, AnimatorStateInfo state)
            {
                var loop = stateName == StableIdle ? _sway.Stable : stateName == UnstableIdle ? _sway.Unstable : null;
                return loop?.AngleAt(Mathf.Repeat(state.normalizedTime, 1f) * loop.Seconds);
            }

            private static string Describe(float? angle)
            {
                return angle.HasValue ? angle.Value.ToString("F4") : "nothing (no clip)";
            }

            private static string Name(AnimatorStateInfo state)
            {
                return state.IsName(Empty) ? Empty
                    : state.IsName(StableIdle) ? StableIdle
                    : state.IsName(UnstableIdle) ? UnstableIdle
                    : "state " + state.shortNameHash;
            }
        }
    }
}
