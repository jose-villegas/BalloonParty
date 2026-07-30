using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>Visible pile for balloons that couldn't spawn; purely visual — HP drain is handled by <see cref="WaveDamageMessage"/>.</summary>
    internal sealed class RejectedBalloonEffect : ITickable, IRunResettable
    {
        private readonly IActiveLevelParameters _levelParams;
        private readonly IOverflowSettings _settings;
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly BalloonPopPresenter _popPresenter;
        private readonly IPublisher<OverflowHeartRequestedMessage> _heartRequestPublisher;
        private readonly SlotGrid _grid;
        private readonly PauseService _pauseService;
        private readonly Dictionary<int, List<OverflowBalloon>> _columns = new();

        private bool _overflowPaused;
        private int _sequenceDepth;
        private int _nextId;
        private float _launchCooldown;
        private int _currentDoomedLineIndex = -1;

        public int ResetOrder => RunResetOrder.Counters;

        // True while the overflow pile is resolving; the level transition waits for this to clear.
        internal bool IsOverflowActive => _overflowPaused;

        [Inject]
        internal RejectedBalloonEffect(
            SlotGrid grid,
            IActiveLevelParameters levelParams,
            IOverflowSettings settings,
            IGamePalette palette,
            PoolManager poolManager,
            BalloonPopPresenter popPresenter,
            IPublisher<OverflowHeartRequestedMessage> heartRequestPublisher,
            PauseService pauseService)
        {
            _grid = grid;
            _levelParams = levelParams;
            _settings = settings;
            _palette = palette;
            _poolManager = poolManager;
            _popPresenter = popPresenter;
            _heartRequestPublisher = heartRequestPublisher;
            _pauseService = pauseService;
        }

        public void Tick()
        {
            var delta = Time.deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            _launchCooldown -= delta;

            // At most one heart per interval, for the front-most ready balloon, so the pile drains in order.
            OverflowBalloon candidate = null;
            var candidateRow = int.MaxValue;

            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var row = 0; row < queue.Count; row++)
                {
                    var balloon = queue[row];
                    var ready = Advance(column.Key, row, balloon, delta);
                    if (ready && !balloon.Launched && balloon.DoomedLineIndex < 0 && row < candidateRow)
                    {
                        candidate = balloon;
                        candidateRow = row;
                    }
                }
            }

            if (candidate != null && _launchCooldown <= 0f)
            {
                LaunchHeart(candidate);
                _launchCooldown = _settings.PopIntervalSeconds;
            }
        }

        public void ResetRun(int generation)
        {
            ReturnAll();
            _sequenceDepth = 0;
            _launchCooldown = 0f;
            TryReleaseOverflowHold();
        }

        /// <summary>Queues reject feedback for a blocked column; <paramref name="staggerIndex"/> delays its appearance.</summary>
        public void Play(int col, int staggerIndex, IReadOnlyDictionary<string, int> activeCounts)
        {
            var queue = QueueFor(col);
            var rowOffset = queue.Count;
            var entry = _levelParams.Current.PickBalloonEntry(activeCounts);

            if (entry == null)
            {
                // Nothing to visualize; HP drain is handled at the wave level via WaveDamageMessage.
                return;
            }

            var view = _poolManager.Get<BalloonView>(entry.PoolKey);
            var model = BalloonModelFactory.Create(entry, _palette, _levelParams.Current.AllowedColors);
            view.Variant.Initialize(model, _levelParams.Current.AllowedColorsMask);

            // Wire the per-type pop VFX so colorless heavies (Tough/Unbreakable) play their authored burst
            // on drain — without overrides the view falls back to the IHasColor-gated default and shows nothing.
            view.SetHitVfxOverrides(entry.HitVfxOverrides);
            view.Bind(model);

            // One row below target so the tick eases it up into place.
            view.transform.position = RowPosition(col, rowOffset + 1);
            view.transform.localScale = Vector3.zero;

            queue.Add(new OverflowBalloon(entry.PoolKey, view, model, col, _nextId++,
                staggerIndex * _settings.AppearStaggerSeconds, _currentDoomedLineIndex));
            BeginOverflowHold();
        }

        /// <summary>Pops the overflow balloon matching a landed heart trail; no-op if it's already gone.</summary>
        public void OnHeartArrived(int requestId)
        {
            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var i = 0; i < queue.Count; i++)
                {
                    if (queue[i].Id == requestId)
                    {
                        Pop(queue[i]);
                        return;
                    }
                }
            }
        }

        /// <summary>Live world position so an in-flight heart trail can home on it as the pile compacts.</summary>
        public bool TryGetLivePosition(int requestId, out Vector3 position)
        {
            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var i = 0; i < queue.Count; i++)
                {
                    if (queue[i].Id == requestId)
                    {
                        position = queue[i].View.transform.position;
                        return true;
                    }
                }
            }

            position = default;
            return false;
        }

        /// <summary>Brackets a turn's spawn sequence; the overflow hold won't release until it ends and the pile is empty.</summary>
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

        /// <summary>Tags subsequent overflow balloons as part of a doomed line so they aren't auto-popped.</summary>
        internal void BeginDoomedLine(int lineIndex)
        {
            _currentDoomedLineIndex = lineIndex;
        }

        internal void EndDoomedLine()
        {
            _currentDoomedLineIndex = -1;
        }

        /// <summary>Pops every overflow balloon tagged with the given doomed line index simultaneously.</summary>
        internal void PopDoomedLine(int lineIndex)
        {
            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var i = queue.Count - 1; i >= 0; i--)
                {
                    if (queue[i].DoomedLineIndex == lineIndex)
                    {
                        Pop(queue[i]);
                    }
                }
            }
        }

        // Eases the balloon toward its current row (its live index) and runs its arrive-then-linger clock.
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

        private void LaunchHeart(OverflowBalloon balloon)
        {
            balloon.Launched = true;

            // Visual/audio feedback per rejected balloon; HP drain is aggregate via WaveDamageMessage.
            _heartRequestPublisher.Publish(new OverflowHeartRequestedMessage(balloon.Id, balloon.View.transform.position));

            // HP is no longer deferred to heart arrival — pop immediately after the visual cue.
            Pop(balloon);
        }

        // Visual burst only — the hit point and shake were already charged in LaunchHeart. Shares the same
        // presenter as projectile pops and board clears, so colorless heavies burst identically here.
        private void Pop(OverflowBalloon balloon)
        {
            _popPresenter.Present(balloon.Model, balloon.View.transform.position, balloon.View);

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

        // Holds the thrower while overflow is resolving so the player can't fire into a board mid-pile.
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
            public OverflowBalloon(
                string poolKey, BalloonView view, IBalloonModel model, int column, int id,
                float appearDelay, int doomedLineIndex = -1)
            {
                PoolKey = poolKey;
                View = view;
                Model = model;
                Column = column;
                Id = id;
                AppearDelay = appearDelay;
                DoomedLineIndex = doomedLineIndex;
            }

            public string PoolKey { get; }
            public BalloonView View { get; }
            public IBalloonModel Model { get; }
            public int Column { get; }
            public int Id { get; }
            public int DoomedLineIndex { get; }
            public float AppearDelay { get; set; }
            public bool Arrived { get; set; }
            public float LingerRemaining { get; set; }
            public bool Launched { get; set; }
        }
    }
}
