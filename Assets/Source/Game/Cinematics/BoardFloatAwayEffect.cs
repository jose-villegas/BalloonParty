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
        public void Collect()
        {
            _views.Clear();
            _balloonRegistry.DetachOutgoing(_scenarioRoot.OutgoingBalloons, _views);
        }

        public float EstimateSeconds()
        {
            if (_views.Count == 0)
            {
                return 0f;
            }

            var settings = _settings.BoardFloatAway;
            return settings.StartDelay + settings.FloatDuration;
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
                // Kick in a bit late — the Ascent is already playing; this is a separate, concurrent beat.
                await UniTask.Delay(TimeSpan.FromSeconds(settings.StartDelay), ignoreTimeScale: true, cancellationToken: ct);

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
            var start = actor.localPosition;

            // Random phase desyncs the balloons and randomizes each one's initial sway direction.
            var phase = UnityEngine.Random.value * (2f * Mathf.PI);
            var swayTurns = settings.ZigzagFrequency * (2f * Mathf.PI);
            var progress = 0f;

            var rise = DOTween.To(() => progress, value => progress = value, 1f, settings.FloatDuration)
                .SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    var offset = settings.ZigzagAmplitude * Mathf.Sin(phase + progress * swayTurns);
                    actor.localPosition = start + new Vector3(offset, settings.RiseHeight * progress, 0f);
                });

            var sequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(rise)
                .Join(actor.DOScale(Vector3.zero, settings.FloatDuration).SetEase(Ease.InQuad));

            view.TweenTracker.Append(sequence);
        }
    }
}
