using System.Collections.Generic;
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
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Feedback for balloons that couldn't spawn: each rises from below the grid into the overflow
    ///     rows and lingers there as a visible pile, then pops after a short linger — and the hit point
    ///     is charged at that pop (the linger-then-burst is the drama beat). The pile is a per-column
    ///     queue — a balloon's target row is its index in the
    ///     column, so when one pops the balloons below it slide up to fill the gap. Transients are pooled
    ///     <see cref="BalloonView"/>s with no grid slot, so <see cref="ResetRun"/> returns them itself.
    /// </summary>
    internal sealed class RejectedBalloonEffect : ITickable, IRunResettable
    {
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IOverflowSettings _settings;
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly IPublisher<SpawnBlockedMessage> _spawnBlockedPublisher;
        private readonly SlotGrid _grid;
        private readonly PauseService _pauseService;
        private readonly Dictionary<int, List<OverflowBalloon>> _columns = new();

        private bool _overflowPaused;
        private int _sequenceDepth;
        private float _popCooldown;

        public int ResetOrder => RunResetOrder.Counters;

        // True while the overflow pile is resolving (the thrower-lock is held): pops still pending.
        // The heart-drain cinematic uses this + an empty trail set to know the drain has finished.
        internal bool IsOverflowActive => _overflowPaused;

        [Inject]
        internal RejectedBalloonEffect(
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            IOverflowSettings settings,
            IGamePalette palette,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField,
            IPublisher<SpawnBlockedMessage> spawnBlockedPublisher,
            PauseService pauseService)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _settings = settings;
            _palette = palette;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
            _spawnBlockedPublisher = spawnBlockedPublisher;
            _pauseService = pauseService;
        }

        public void Tick()
        {
            var delta = Time.deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            _popCooldown -= delta;

            // Advance everyone, then pop at most one balloon per interval — the front-most (topmost)
            // ready one — so the pile bursts one after another, never several at once.
            OverflowBalloon ready = null;
            var readyRow = int.MaxValue;

            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var row = 0; row < queue.Count; row++)
                {
                    if (Advance(column.Key, row, queue[row], delta) && row < readyRow)
                    {
                        ready = queue[row];
                        readyRow = row;
                    }
                }
            }

            if (ready != null && _popCooldown <= 0f)
            {
                Pop(ready);
                _popCooldown = _settings.PopIntervalSeconds;
            }
        }

        public void ResetRun(int generation)
        {
            ReturnAll();
            _sequenceDepth = 0;
            _popCooldown = 0f;
            TryReleaseOverflowHold();
        }

        /// <summary>
        ///     Queues the reject feedback for a blocked column: a would-be balloon enters at the next
        ///     free overflow row below the grid, lingers, and costs one hit point when it pops. <paramref
        ///     name="staggerIndex"/> delays its appearance so a line sweeps; <paramref name="activeCounts"/>
        ///     is read (not owned) so the would-be balloon honours the same per-type caps as a real spawn.
        /// </summary>
        public void Play(int col, int staggerIndex, IReadOnlyDictionary<string, int> activeCounts)
        {
            var queue = QueueFor(col);
            var rowOffset = queue.Count;
            var entry = _balloonsConfig.Entries.PickRandom(activeCounts);

            if (entry == null)
            {
                // No type to visualize, but the column is still blocked — charge the hit point anyway.
                _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(col, RowPosition(col, rowOffset)));
                return;
            }

            var view = _poolManager.Get<BalloonView>(entry.PoolKey);
            var model = BalloonModelFactory.Create(entry, _palette);
            view.Variant.Initialize(model);
            view.Bind(model);

            // Enter one row below the target so the tick eases it up into place.
            view.transform.position = RowPosition(col, rowOffset + 1);
            view.transform.localScale = Vector3.zero;

            queue.Add(new OverflowBalloon(entry.PoolKey, view, col, staggerIndex * _settings.AppearStaggerSeconds));
            BeginOverflowHold();
        }

        /// <summary>
        ///     Brackets a turn's whole spawn sequence (which may spawn several lines with delays between
        ///     them). The overflow hold can't release until the sequence is over <em>and</em> the pile is
        ///     empty, so the thrower stays locked across the gaps between lines.
        /// </summary>
        public void BeginSpawnSequence()
        {
            _sequenceDepth++;
        }

        public void EndSpawnSequence()
        {
            if (_sequenceDepth > 0)
            {
                _sequenceDepth--;
            }

            TryReleaseOverflowHold();
        }

        // Eases one balloon toward its current row (= its index, so compaction is automatic) and runs its
        // arrive→linger clock. Returns true once it's eligible to pop (the Tick spaces the actual pops).
        private bool Advance(int col, int rowOffset, OverflowBalloon balloon, float delta)
        {
            if (balloon.AppearDelay > 0f)
            {
                balloon.AppearDelay -= delta;
                return false;
            }

            var transform = balloon.View.transform;
            var target = RowPosition(col, rowOffset);
            var t = 1f - Mathf.Exp(-_settings.MoveSharpness * delta);
            transform.position = Vector3.Lerp(transform.position, target, t);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, t);

            if (!balloon.Arrived && transform.position.WithinRadius(target, _settings.ArrivalRadius))
            {
                transform.position = target;
                balloon.Arrived = true;
                balloon.LingerRemaining = _settings.LingerSeconds;
            }

            if (!balloon.Arrived)
            {
                return false;
            }

            balloon.LingerRemaining -= delta;
            return balloon.LingerRemaining <= 0f;
        }

        private void Pop(OverflowBalloon balloon)
        {
            var position = balloon.View.transform.position;

            balloon.View.PlayHitVfxForOutcome(HitOutcome.Pop);
            _disturbanceField.Stamp(StampSource.BalloonPop, position, Vector2.zero);

            // Charge the hit point at the pop, not on arrival — the linger-then-burst is the drama beat.
            _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(balloon.Column, position));

            QueueFor(balloon.Column).Remove(balloon);
            _poolManager.Return(balloon.PoolKey, balloon.View);

            TryReleaseOverflowHold();
        }

        private void ReturnAll()
        {
            foreach (var column in _columns)
            {
                foreach (var balloon in column.Value)
                {
                    _poolManager.Return(balloon.PoolKey, balloon.View);
                }

                column.Value.Clear();
            }
        }

        private List<OverflowBalloon> QueueFor(int col)
        {
            if (!_columns.TryGetValue(col, out var queue))
            {
                queue = new List<OverflowBalloon>();
                _columns[col] = queue;
            }

            return queue;
        }

        private Vector3 RowPosition(int col, int rowOffset)
        {
            return _grid.IndexToWorldPosition(new Vector2Int(col, _grid.Rows + rowOffset));
        }

        // Hold the thrower while overflow is resolving so the player can't fire into a board mid-pile.
        // Engaged on the first rejected balloon; released only once the spawn sequence is done and the
        // pile is empty — at which point the run has survived (thrower re-enables) or ended (GameOver
        // keeps it disabled).
        private void BeginOverflowHold()
        {
            if (_overflowPaused)
            {
                return;
            }

            _pauseService.Pause(PauseSource.Overflow);
            _overflowPaused = true;
        }

        private void TryReleaseOverflowHold()
        {
            if (!_overflowPaused || _sequenceDepth > 0 || HasActiveBalloons())
            {
                return;
            }

            _pauseService.Resume(PauseSource.Overflow);
            _overflowPaused = false;
        }

        private bool HasActiveBalloons()
        {
            foreach (var column in _columns)
            {
                if (column.Value.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class OverflowBalloon
        {
            public OverflowBalloon(string poolKey, BalloonView view, int column, float appearDelay)
            {
                PoolKey = poolKey;
                View = view;
                Column = column;
                AppearDelay = appearDelay;
            }

            public string PoolKey { get; }
            public BalloonView View { get; }
            public int Column { get; }
            public float AppearDelay { get; set; }
            public bool Arrived { get; set; }
            public float LingerRemaining { get; set; }
        }
    }
}
