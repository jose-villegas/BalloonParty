using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
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
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Feedback for balloons that couldn't spawn: each rises from below the grid into the overflow
    ///     rows and lingers there as a visible pile. Draining is heart-driven — when a balloon is ready a
    ///     heart trail is requested toward it (<see cref="OverflowHeartRequestedMessage"/>) and the hit
    ///     point + camera shake are charged right then, at the launch; the balloon itself only pops (the
    ///     visual burst) once that heart lands via <see cref="OnHeartArrived"/>.
    ///     The pile is a per-column queue whose target row is the live list index, so when one pops the
    ///     balloons below slide up to fill the gap; in-flight hearts home on the balloon's live position
    ///     (<see cref="TryGetLivePosition"/>), so they still land on it as it compacts. Transients are
    ///     pooled <see cref="BalloonView"/>s with no grid slot, so <see cref="ResetRun"/> returns them itself.
    /// </summary>
    internal sealed class RejectedBalloonEffect : ITickable, IRunResettable, IPendingHealthCharges
    {
        private readonly IActiveLevelParameters _levelParams;
        private readonly IOverflowSettings _settings;
        private readonly IGamePalette _palette;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly IPublisher<SpawnBlockedMessage> _spawnBlockedPublisher;
        private readonly IPublisher<OverflowHeartRequestedMessage> _heartRequestPublisher;
        private readonly SlotGrid _grid;
        private readonly PauseService _pauseService;
        private readonly Dictionary<int, List<OverflowBalloon>> _columns = new();

        private bool _overflowPaused;
        private int _sequenceDepth;
        private int _nextId;
        private float _launchCooldown;

        public int ResetOrder => RunResetOrder.Counters;

        // True while the overflow pile is resolving (the thrower-lock is held): balloons still pending.
        // The heart-drain cinematic uses this + an empty trail set to know the drain has finished.
        internal bool IsOverflowActive => _overflowPaused;

        // Every queued balloon will unconditionally cost one HP when its heart launches — the loss
        // forecast reads this to know a loss is certain at reject-queue time, not at the Nth launch.
        public int PendingCharges
        {
            get
            {
                var pending = 0;
                foreach (var column in _columns)
                {
                    var queue = column.Value;
                    for (var i = 0; i < queue.Count; i++)
                    {
                        if (!queue[i].Launched)
                        {
                            pending++;
                        }
                    }
                }

                return pending;
            }
        }

        [Inject]
        internal RejectedBalloonEffect(
            SlotGrid grid,
            IActiveLevelParameters levelParams,
            IOverflowSettings settings,
            IGamePalette palette,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField,
            IPublisher<SpawnBlockedMessage> spawnBlockedPublisher,
            IPublisher<OverflowHeartRequestedMessage> heartRequestPublisher,
            PauseService pauseService)
        {
            _grid = grid;
            _levelParams = levelParams;
            _settings = settings;
            _palette = palette;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
            _spawnBlockedPublisher = spawnBlockedPublisher;
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

            // Advance everyone, then request at most one heart per interval — for the front-most (topmost)
            // ready balloon that hasn't launched yet — so the pile drains front-first, one at a time, and
            // the rest compact up behind it.
            OverflowBalloon candidate = null;
            var candidateRow = int.MaxValue;

            foreach (var column in _columns)
            {
                var queue = column.Value;
                for (var row = 0; row < queue.Count; row++)
                {
                    var balloon = queue[row];
                    var ready = Advance(column.Key, row, balloon, delta);
                    if (ready && !balloon.Launched && row < candidateRow)
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

        /// <summary>
        ///     Queues the reject feedback for a blocked column: a would-be balloon enters at the next free
        ///     overflow row below the grid and lingers there. <paramref name="staggerIndex"/> delays its
        ///     appearance so a line sweeps; <paramref name="activeCounts"/> is read (not owned) so the
        ///     would-be balloon honours the same per-type caps as a real spawn.
        /// </summary>
        public void Play(int col, int staggerIndex, IReadOnlyDictionary<string, int> activeCounts)
        {
            var queue = QueueFor(col);
            var rowOffset = queue.Count;
            var entry = _levelParams.PickBalloonEntry(activeCounts);

            if (entry == null)
            {
                // No type to visualize, but the column is still blocked — charge the hit point anyway.
                _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(col, RowPosition(col, rowOffset)));
                return;
            }

            var view = _poolManager.Get<BalloonView>(entry.PoolKey);
            var model = BalloonModelFactory.Create(entry, _palette, _levelParams.AllowedColors);
            view.Variant.Initialize(model, _levelParams.AllowedColorsMask);
            view.Bind(model);

            // Enter one row below the target so the tick eases it up into place.
            view.transform.position = RowPosition(col, rowOffset + 1);
            view.transform.localScale = Vector3.zero;

            queue.Add(new OverflowBalloon(entry.PoolKey, view, col, _nextId++, staggerIndex * _settings.AppearStaggerSeconds));
            BeginOverflowHold();
        }

        /// <summary>
        ///     Pops the overflow balloon a landed heart trail was flying to (matched by id) — the drama
        ///     beat: the hit point is charged here, at the pop, not when the balloon first went ready.
        ///     A no-op if the balloon is already gone (e.g. the run reset out from under an in-flight heart).
        /// </summary>
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

        /// <summary>
        ///     The current world position of the overflow balloon with <paramref name="requestId"/>, so an
        ///     in-flight heart trail can home on it as the pile compacts. False once the balloon is gone.
        /// </summary>
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

        // Eases one balloon toward its current row (= its live index, so it compacts up when a balloon
        // ahead pops) and runs its arrive→linger clock. Returns true once it's ready for its heart (the
        // Tick spaces the actual requests); in-flight hearts track the moving balloon, so compaction is safe.
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
            var position = balloon.View.transform.position;

            // The heart request goes out first so the trail (and the drain cinematic) exists while the
            // run is still in Game; the charge follows immediately — the hit point and camera shake land
            // the moment the heart leaves the UI, not when it arrives. A 0-HP game-over triggered here
            // simply ends the cinematic early (extra hearts past 0 don't count).
            _heartRequestPublisher.Publish(new OverflowHeartRequestedMessage(balloon.Id, position));
            _spawnBlockedPublisher.Publish(new SpawnBlockedMessage(balloon.Column, position));
        }

        // Purely the visual burst — the hit point and shake were already charged when the heart launched
        // (see LaunchHeart); the landing just resolves the balloon it paid for.
        private void Pop(OverflowBalloon balloon)
        {
            var position = balloon.View.transform.position;

            balloon.View.PlayHitVfxForOutcome(HitOutcome.Pop);
            _disturbanceField.Stamp(StampSource.BalloonPop, position, Vector2.zero);

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
            public OverflowBalloon(string poolKey, BalloonView view, int column, int id, float appearDelay)
            {
                PoolKey = poolKey;
                View = view;
                Column = column;
                Id = id;
                AppearDelay = appearDelay;
            }

            public string PoolKey { get; }
            public BalloonView View { get; }
            public int Column { get; }
            public int Id { get; }
            public float AppearDelay { get; set; }
            public bool Arrived { get; set; }
            public float LingerRemaining { get; set; }
            public bool Launched { get; set; }
        }
    }
}
