using System;
using BalloonParty.Audio;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using BalloonParty.UI.Score;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Spawns one heart trail per heart lost when a <see cref="WaveDamageMessage"/> arrives.
    ///     Each trail flies from the health UI to the doomed line, slides back and forth (strikethrough),
    ///     then pops all overflow balloons in that line simultaneously.
    /// </summary>
    internal sealed class HeartTrailController : IStartable, IDisposable
    {
        private const string TrailPoolKey = "HeartTrail";

        private readonly IOverflowSettings _settings;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly IPublisher<StrikethroughArrivedMessage> _strikethroughPublisher;
        private readonly ISoundPlayer _soundPlayer;
        private readonly PoolManager _poolManager;
        private readonly FlyingTrail _prefab;
        private readonly TrailEndpointRegistry _endpoints;
        private readonly HeartTrailTracker _tracker;
        private readonly SlotGrid _grid;
        private readonly RejectedBalloonEffect _overflow;

        private IDisposable _subscription;
        private TrailSpawner _spawner;

        [Inject]
        internal HeartTrailController(
            IOverflowSettings settings,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber,
            IPublisher<StrikethroughArrivedMessage> strikethroughPublisher,
            ISoundPlayer soundPlayer,
            PoolManager poolManager,
            FlyingTrail prefab,
            TrailEndpointRegistry endpoints,
            HeartTrailTracker tracker,
            SlotGrid grid,
            RejectedBalloonEffect overflow)
        {
            _settings = settings;
            _waveDamageSubscriber = waveDamageSubscriber;
            _strikethroughPublisher = strikethroughPublisher;
            _soundPlayer = soundPlayer;
            _poolManager = poolManager;
            _prefab = prefab;
            _endpoints = endpoints;
            _tracker = tracker;
            _grid = grid;
            _overflow = overflow;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, _prefab);
            _subscription = _waveDamageSubscriber.Subscribe(OnWaveDamage);
        }

        private void OnWaveDamage(WaveDamageMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Heart, out var source))
            {
                return;
            }

            var from = source.Center;

            for (var i = 0; i < msg.HeartsLost; i++)
            {
                SpawnStrikethrough(from, i, i * _settings.StrikethroughStaggerDelay);
            }
        }

        private void SpawnStrikethrough(Vector3 from, int lineIndex, float delay)
        {
            var overflowRow = _grid.Rows + lineIndex;
            var leftPos = _grid.IndexToWorldPosition(new Vector2Int(0, overflowRow));
            var rightPos = _grid.IndexToWorldPosition(new Vector2Int(_grid.Columns - 1, overflowRow));

            var trail = _spawner.Acquire(Color.white);
            trail.transform.position = from;
            trail.ClearRibbon();
            trail.SetRibbonEmitting(false);
            _tracker.Add(trail.transform);

            var jitter = _settings.StrikethroughJitter;
            var passDuration = _settings.StrikethroughPassDuration;

            var seq = DOTween.Sequence();

            // Stagger: wait for previous strikethroughs to finish.
            if (delay > 0f)
            {
                seq.AppendInterval(delay);
            }

            // Enable ribbon and play sound just before the flight begins.
            var capturedLineIndex = lineIndex;
            seq.AppendCallback(() =>
            {
                trail.SetRibbonEmitting(true);
                _soundPlayer.Play(GameSoundId.Strikethrough, leftPos,
                    semitoneOffset: -capturedLineIndex * 3);
            });

            // Phase 1: fly from hearts UI to left edge of the doomed line.
            seq.Append(trail.transform.DOMove(leftPos, _settings.HeartTrailDuration).SetEase(Ease.OutCubic));

            // Phase 2: back-and-forth passes across the line with jitter.
            for (var pass = 0; pass < _settings.StrikethroughPasses; pass++)
            {
                var isRightward = pass % 2 == 0;
                var target = isRightward ? rightPos : leftPos;
                var jittered = target + new Vector3(
                    UnityEngine.Random.Range(-jitter, jitter),
                    UnityEngine.Random.Range(-jitter, jitter),
                    0f);
                seq.Append(trail.transform.DOMove(jittered, passDuration).SetEase(Ease.InOutSine));
            }

            seq.OnComplete(() =>
            {
                _overflow.PopDoomedLine(capturedLineIndex);
                _strikethroughPublisher.Publish(new StrikethroughArrivedMessage(capturedLineIndex));
                _tracker.Remove(trail.transform);
                _spawner.Release(trail);
            });
        }
    }
}
