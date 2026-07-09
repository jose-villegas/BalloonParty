using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Slots.Actor;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Substitutes the pop wave: detaches the old balloons into the transition's outgoing group (off the
    ///     grid, out of logic, reparented under <see cref="ScenarioContentRoot.OutgoingBalloons" /> so they
    ///     travel with the fake-camera descent), then floats each up on a phase-randomized sine while scaling
    ///     to zero, and hands them back to the pool. The level-up alternative to <see cref="BoardPopWave" />.
    /// </summary>
    internal sealed class BoardFloatAwayEffect : IBoardEffect
    {
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly ICinematicsSettings _settings;
        private readonly List<ISlotActorView> _views = new();

        internal BoardFloatAwayEffect(
            BalloonControllerRegistry balloonRegistry,
            ScenarioContentRoot scenarioRoot,
            ICinematicsSettings settings)
        {
            _balloonRegistry = balloonRegistry;
            _scenarioRoot = scenarioRoot;
            _settings = settings;
        }

        // Graduates the old balloons into the outgoing group (detach + reparent under the root's
        // OutgoingBalloons holder), keeping their views to animate. Call after the root is reset to origin.
        // exitDrop compensates the reparent for the descent's root lift (supplied by the transition).
        public void Collect(float exitDrop)
        {
            _views.Clear();
            _balloonRegistry.DetachOutgoing(_scenarioRoot.OutgoingBalloons, exitDrop, _views);
        }

        public float EstimateSeconds()
        {
            if (_views.Count == 0)
            {
                return 0f;
            }

            return _settings.BoardFloatAway.FloatDuration;
        }

        public async UniTask PlayAsync(CancellationToken ct)
        {
            if (_views.Count == 0)
            {
                return;
            }

            var settings = _settings.BoardFloatAway;
            try
            {
                foreach (var view in _views)
                {
                    FloatOne(view, settings);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(settings.FloatDuration), ignoreTimeScale: true, cancellationToken: ct);
            }
            finally
            {
                // Hand every view back in one go — no reliance on per-tween callbacks, which a pool despawn
                // would kill mid-flight; the finally also covers a cancelled transition.
                _balloonRegistry.ReturnOutgoing();
                _views.Clear();
            }
        }

        private void FloatOne(ISlotActorView view, BoardFloatAwaySettings settings)
        {
            var actor = view.transform;
            var pivot = view.RotationPivot;
            var start = actor.localPosition;

            // Random phase desyncs the balloons and randomizes each one's initial sway direction.
            var phase = UnityEngine.Random.value * (2f * Mathf.PI);
            var swayTurns = settings.ZigzagFrequency * (2f * Mathf.PI);

            // Per-balloon rise scale so they don't all top out at the same height.
            var riseScale = 1f + UnityEngine.Random.Range(-settings.RiseVariance, settings.RiseVariance);
            var progress = 0f;

            var rise = DOTween.To(() => progress, value => progress = value, 1f, settings.FloatDuration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => ApplyStep(actor, pivot, start, settings, phase, swayTurns, riseScale, progress));

            var sequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(rise);

            view.TweenTracker.Append(sequence);
        }

        // Drives position and lean off one shared sine sample so the balloon tilts into whichever way it
        // sways. Position moves the whole actor; the tilt rides a pivot so lighting-baked sprites parented
        // outside it stay put.
        private static void ApplyStep(
            Transform actor,
            Transform pivot,
            Vector3 start,
            BoardFloatAwaySettings settings,
            float phase,
            float swayTurns,
            float riseScale,
            float progress)
        {
            // Ramp the sway with progress so both offset and tilt ease out of the original pose, no snap at t=0.
            var swing = progress * Mathf.Sin(phase + progress * swayTurns);
            var rise = settings.RiseCurve.Evaluate(progress * settings.FloatDuration) * riseScale;
            actor.localPosition = start + new Vector3(settings.ZigzagAmplitude * swing, rise, 0f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, -settings.SwayTiltAngle * swing);
        }
    }
}
