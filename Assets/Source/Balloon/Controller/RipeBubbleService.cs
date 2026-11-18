using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Ripens bubble clusters that reach the merge cap (<see cref="RipeBubbleRule" />) and runs each one's fuse on
    ///     the level clock (<see cref="ISpawnClock.SecondsOnLevel" />), which holds through pause, the level-up and
    ///     game over. A ripe cluster popped by a hit bursts as the player's ripple (<see cref="RipeBubbleBurst.Ripple" />)
    ///     <see cref="IRipeBubbleSettings.BurstSeconds" /> after the hit, as the big bubble its view folds into pops; a
    ///     ripe neighbour the ripple pops does the same in turn. One whose fuse runs out hands its bubbles to its
    ///     neighbours and leaves the board, the level's foam (<see cref="RipeBubbleBurst.PlanFoam" />,
    ///     <see cref="RipeBubbleBurst.HandOut" />). Either way a <see cref="RipeBubbleBurstMessage" /> goes out, and
    ///     each throb of a fuse sends a <see cref="RipeFuseBeatMessage" />. Looks for new ripe clusters when anything
    ///     could have filled one: the grid changing, a cluster being hit, a merge, or a hand-out. A level change, game
    ///     over or run reset forgets every fuse and every burst still waiting.
    /// </summary>
    internal sealed class RipeBubbleService : IStartable, ITickable, IRunResettable, IDisposable
    {
        // A starting size, not a limit: room for every cluster a board holds at once (BubbleRiseSettings.MaxBubbles,
        // 12 shipped), so tracking them never has to grow the lists.
        private const int Capacity = 16;

        private readonly SlotGrid _grid;
        private readonly ISpawnClock _clock;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IRipeBubbleSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly RipeBubbleBurst _burst;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<BubbleMergedMessage> _mergedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly IPublisher<BubbleRipenedMessage> _ripenedPublisher;
        private readonly IPublisher<RipeBubbleBurstMessage> _burstPublisher;
        private readonly IPublisher<RipeFuseBeatMessage> _beatPublisher;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<RipeFuse> _fuses = new(Capacity);
        private readonly List<RipeFuse> _expired = new(Capacity);
        private readonly List<PendingBurst> _pendingBursts = new(Capacity);

        private int _seenVersion = -1;

        // A merge publishes just before the counts change, so one heard now is looked at on the next tick.
        private bool _woken;

        // Counts Stop calls, so a drain can tell that a ripple's hits ended the level under it.
        private int _stops;

        public int ResetOrder => RunResetOrder.Quiesce;

        internal int Tracked => _fuses.Count;
        internal int Pending => _pendingBursts.Count;

        [Inject]
        internal RipeBubbleService(SlotGrid grid, ISpawnClock clock, IBalloonsConfiguration balloonsConfig,
            ILevelProgress levelProgress, RipeBubbleBurst burst, ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<BubbleMergedMessage> mergedSubscriber, ISubscriber<GameOverMessage> gameOverSubscriber,
            IPublisher<BubbleRipenedMessage> ripenedPublisher, IPublisher<RipeBubbleBurstMessage> burstPublisher,
            IPublisher<RipeFuseBeatMessage> beatPublisher)
        {
            _grid = grid;
            _clock = clock;
            _balloonsConfig = balloonsConfig;
            _settings = balloonsConfig.RipeBubble;
            _levelProgress = levelProgress;
            _burst = burst;
            _hitSubscriber = hitSubscriber;
            _mergedSubscriber = mergedSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _ripenedPublisher = ripenedPublisher;
            _burstPublisher = burstPublisher;
            _beatPublisher = beatPublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _mergedSubscriber.Subscribe(_ => _woken = true).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(_ => Stop()).AddTo(_subscriptions);
            // Skip the value the property replays on subscribing: only a change of level stops the fuses.
            _levelProgress.Level.Skip(1).Subscribe(_ => Stop()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            // A ripple whose hits ended the level or the run leaves the rest of the tick undone: Stop has cleared
            // everything, and ripening clusters again here would undo it before the next tick looks.
            if (_pendingBursts.Count > 0 && !DrainPendingBursts())
            {
                return;
            }

            if (_settings.FuseSeconds <= 0f)
            {
                return;
            }

            if (_woken || _grid.MutationVersion != _seenVersion)
            {
                _woken = false;
                _seenVersion = _grid.MutationVersion;
                RipenFullClusters();
            }

            if (_fuses.Count > 0)
            {
                AdvanceFuses();
            }
        }

        public void ResetRun(int generation)
        {
            Stop();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Never dispatches a hit: a ripe pop is heard inside the resolver's own dispatch, which reads the shot's
        // streak straight after it, or inside a ripple's dispatch. The burst is queued for Tick, due when the big
        // bubble the view folds into pops; a chain waits as long at each link, so it reads as one.
        private void OnActorHit(ActorHitMessage message)
        {
            if (message.Actor is not BubbleClusterModel cluster)
            {
                return;
            }

            _woken = true;
            if ((message.Outcome & HitOutcome.Pop) == 0)
            {
                return;
            }

            var index = IndexOf(cluster);
            if (index < 0)
            {
                return;
            }

            _fuses.RemoveAt(index);
            var due = _clock.SecondsOnLevel + _settings.BurstSeconds;
            _pendingBursts.Add(new PendingBurst(cluster.SlotIndex.Value, message.Context, due));
        }

        // Index loop, re-reading Count: a ripple's hits can pop a ripe neighbour, whose burst is appended to this
        // list while it is drained. The ripple is only ever called from here, so a chain is a loop, never a
        // recursion. A due burst leaves the list before its ripple runs, so one whose hits throw cannot run again
        // on the next tick; bursts not yet due stay where they are. False when a ripple's hits stopped the service.
        private bool DrainPendingBursts()
        {
            var stops = _stops;
            var now = _clock.SecondsOnLevel;
            var i = 0;
            while (i < _pendingBursts.Count)
            {
                var burst = _pendingBursts[i];
                if (burst.Due > now)
                {
                    i++;
                    continue;
                }

                _pendingBursts.RemoveAt(i);
                var reached = _burst.Ripple(burst.Slot, burst.Context);
                if (stops != _stops)
                {
                    // The ripple's hits ended the level or the run: Stop has emptied the list already.
                    return false;
                }

                _burstPublisher.Publish(new RipeBubbleBurstMessage(
                    RipeBurstKind.Ripple, burst.Slot, _grid.IndexToWorldPosition(burst.Slot), reached));
            }

            return true;
        }

        private void RipenFullClusters()
        {
            var max = _balloonsConfig.BubbleMergeMax;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is BubbleClusterModel cluster && !cluster.Ripe.Value
                        && RipeBubbleRule.IsRipe(cluster.HitsRemaining.Value, max))
                    {
                        Ripen(cluster, slot);
                    }
                }
            }
        }

        private void Ripen(BubbleClusterModel cluster, Vector2Int slot)
        {
            cluster.Ripen();
            cluster.SetFuseLeft(_settings.FuseSeconds);
            _fuses.Add(new RipeFuse(cluster, _clock.SecondsOnLevel + _settings.FuseSeconds));
            _ripenedPublisher.Publish(new BubbleRipenedMessage(slot, _grid.IndexToWorldPosition(slot)));
        }

        // The model is tracked, not its slot, so a cluster the balancer moves keeps its fuse. Fuses that run out
        // are only collected here and spent after the loop, so whatever spending them sets off cannot change
        // _fuses while it is walked.
        private void AdvanceFuses()
        {
            var now = _clock.SecondsOnLevel;
            for (var i = _fuses.Count - 1; i >= 0; i--)
            {
                var fuse = _fuses[i];
                var cluster = fuse.Cluster;
                if (!ReferenceEquals(_grid.At(cluster.SlotIndex.Value), cluster))
                {
                    _fuses.RemoveAt(i);
                    cluster.Unripen();
                    continue;
                }

                var before = cluster.FuseLeft.Value;
                var left = RipeBubbleRule.FuseLeft(fuse.Deadline, now);
                cluster.SetFuseLeft(left);
                if (left > 0f)
                {
                    if (RipeBubbleRule.Throbs(before, left, _settings.ThrobFastSeconds))
                    {
                        var slot = cluster.SlotIndex.Value;
                        _beatPublisher.Publish(new RipeFuseBeatMessage(
                            slot, _grid.IndexToWorldPosition(slot),
                            RipeBubbleRule.IsFastThrob(left, _settings.ThrobFastSeconds)));
                    }

                    continue;
                }

                _fuses.RemoveAt(i);
                _expired.Add(fuse);
            }

            if (_expired.Count > 0)
            {
                SprayFoam();
            }
        }

        // Taken off the end, so each entry leaves the list before it is handed out: a Stop heard while the foam is
        // announced unripens only the clusters still waiting. AdvanceFuses walked the fuses backwards, so this
        // sprays them in the order they were tracked. A cluster that left its slot or stopped being ripe since
        // its fuse was read is only cleared. The message goes out before the hand-out changes the board, so the
        // views still find the cluster and its takers as they were; the hand-out then runs whatever the message
        // set off, as it promised. The cluster stays ripe as it leaves, so its view keeps the warning colour.
        private void SprayFoam()
        {
            while (_expired.Count > 0)
            {
                var last = _expired.Count - 1;
                var cluster = _expired[last].Cluster;
                _expired.RemoveAt(last);
                var slot = cluster.SlotIndex.Value;
                if (!cluster.Ripe.Value || !ReferenceEquals(_grid.At(slot), cluster))
                {
                    cluster.Unripen();
                    continue;
                }

                var handout = _burst.PlanFoam(slot, cluster.HitsRemaining.Value);
                _burstPublisher.Publish(new RipeBubbleBurstMessage(
                    RipeBurstKind.Foam, slot, _grid.IndexToWorldPosition(slot),
                    RipeBubbleRule.CoatedNeighbours(handout), handout));
                _burst.HandOut(cluster, handout);
                // A neighbouring cluster the hand-out filled ripens on a later tick.
                _woken = true;
            }
        }

        private int IndexOf(BubbleClusterModel cluster)
        {
            for (var i = 0; i < _fuses.Count; i++)
            {
                if (ReferenceEquals(_fuses[i].Cluster, cluster))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Stop()
        {
            foreach (var fuse in _fuses)
            {
                fuse.Cluster.Unripen();
            }

            foreach (var fuse in _expired)
            {
                fuse.Cluster.Unripen();
            }

            _fuses.Clear();
            _expired.Clear();
            _pendingBursts.Clear();
            _stops++;
            // Look again on the next tick: a cluster still at the cap on a board that does not change would
            // otherwise stay unripe.
            _woken = true;
        }

        private readonly struct RipeFuse
        {
            public readonly BubbleClusterModel Cluster;
            public readonly float Deadline;

            public RipeFuse(BubbleClusterModel cluster, float deadline)
            {
                Cluster = cluster;
                Deadline = deadline;
            }
        }

        // The hit that popped the cluster rides along, so the ripple's hits carry the same shot and score into
        // its streak.
        private readonly struct PendingBurst
        {
            public readonly Vector2Int Slot;
            public readonly DamageContext Context;
            public readonly float Due;

            public PendingBurst(Vector2Int slot, DamageContext context, float due)
            {
                Slot = slot;
                Context = context;
                Due = due;
            }
        }
    }
}
