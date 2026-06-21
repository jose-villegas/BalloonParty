using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Feedback for a balloon that could not spawn: a would-be balloon rises from the entry, pops
    ///     just below the grid, and publishes <see cref="SpawnBlockedMessage"/> at the pop (which costs
    ///     the player a hit point). Each transient is a pooled <see cref="BalloonView"/> with no grid
    ///     slot or controller, so <see cref="ResetRun"/> returns any mid-pop ones itself — the
    ///     board-clear broadcast can't reach them.
    /// </summary>
    internal class RejectedBalloonEffect : IRunResettable, IDisposable
    {
        // Visual-only delay between the pops of several balloons rejected in the same line, so the
        // feedback reads as a sequence of hits rather than one stacked flash.
        private const float StaggerSeconds = 0.08f;

        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly IPublisher<SpawnBlockedMessage> _spawnBlockedPublisher;
        private readonly SlotGrid _grid;
        private readonly PauseService _pauseService;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<(string PoolKey, BalloonView View)> _active = new();

        private int _generation;
        private bool _overflowPaused;

        public int ResetOrder => RunResetOrder.Counters;

        [Inject]
        internal RejectedBalloonEffect(
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            IGamePalette palette,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField,
            IPublisher<SpawnBlockedMessage> spawnBlockedPublisher,
            PauseService pauseService)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _palette = palette;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
            _spawnBlockedPublisher = spawnBlockedPublisher;
            _pauseService = pauseService;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        public void ResetRun(int generation)
        {
            // Adopt the run's generation so mid-stagger plays drop themselves, then return any
            // transient that already has a view (its tween is killed on despawn).
            _generation = generation;

            foreach (var (poolKey, view) in _active)
            {
                _poolManager.Return(poolKey, view);
            }

            _active.Clear();
            EndOverflowHold();
        }

        /// <summary>
        ///     Plays the reject feedback for a blocked column and charges one hit point at the pop.
        ///     <paramref name="staggerIndex"/> spaces out multiple rejects in the same line;
        ///     <paramref name="activeCounts"/> is read (not owned) so the would-be balloon honours the
        ///     same per-type caps as a real spawn.
        /// </summary>
        public void Play(int col, int staggerIndex, IReadOnlyDictionary<string, int> activeCounts)
        {
            PlayAsync(col, staggerIndex, _generation, activeCounts).Forget();
        }

        private async UniTaskVoid PlayAsync(
            int col,
            int staggerIndex,
            int generation,
            IReadOnlyDictionary<string, int> activeCounts)
        {
            if (staggerIndex > 0)
            {
                await UniTask.Delay(
                    (int)(StaggerSeconds * staggerIndex * 1000),
                    cancellationToken: _cts.Token);

                if (generation != _generation)
                {
                    return;
                }
            }

            // Pop below the grid, not inside it: the would-be balloon rises from the entry to just
            // beneath the bottom row and bursts there, so it never overlaps the packed board.
            var popPosition = _grid.IndexToWorldPosition(new Vector2Int(col, _grid.Rows));
            var entry = _balloonsConfig.Entries.PickRandom(activeCounts);

            if (entry == null)
            {
                // No type available to visualize, but the column is still blocked — bleed anyway.
                _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(col, popPosition));
                return;
            }

            var appearPosition = _grid.IndexToWorldPosition(new Vector2Int(col, _grid.Rows + 1));

            var view = _poolManager.Get<BalloonView>(entry.PoolKey);
            var model = BalloonModelFactory.Create(entry, _palette);
            view.Variant.Initialize(model);
            view.Bind(model);

            view.transform.position = appearPosition;
            view.transform.localScale = Vector3.zero;
            _active.Add((entry.PoolKey, view));
            BeginOverflowHold();

            var duration = UnityEngine.Random.Range(
                _balloonsConfig.BalloonSpawnAnimationDurationRange.x,
                _balloonsConfig.BalloonSpawnAnimationDurationRange.y);

            var sequence = DOTween.Sequence();
            sequence.Join(view.transform.DOScale(Vector3.one, duration));
            sequence.Join(view.transform.DOMove(popPosition, duration));
            sequence.OnComplete(() => Complete(col, entry.PoolKey, view, popPosition));
            view.TweenTracker.Replace(sequence);
        }

        private void Complete(int col, string poolKey, BalloonView view, Vector3 popPosition)
        {
            view.PlayHitVfxForOutcome(HitOutcome.Pop);
            _disturbanceField.Stamp(StampSource.BalloonPop, popPosition, Vector2.zero);
            _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(col, popPosition));

            _active.Remove((poolKey, view));
            _poolManager.Return(poolKey, view);

            if (_active.Count == 0)
            {
                EndOverflowHold();
            }
        }

        // Hold the thrower for the duration of a turn's overflow pops so the player can't fire into a
        // board that's still resolving. Released when the last rejected balloon finishes — at which
        // point the run has either survived (thrower re-enables) or ended (GameOver keeps it disabled).
        private void BeginOverflowHold()
        {
            if (_overflowPaused)
            {
                return;
            }

            _pauseService.Pause(PauseSource.Overflow);
            _overflowPaused = true;
        }

        private void EndOverflowHold()
        {
            if (!_overflowPaused)
            {
                return;
            }

            _pauseService.Resume(PauseSource.Overflow);
            _overflowPaused = false;
        }
    }
}
