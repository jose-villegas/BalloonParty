using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Nudge;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     The nuke going off (PLAN-NukeBalloon), when its fuse runs out (<see cref="NukeFuse" />). The nuke pops and
    ///     every shot in flight dies (<see cref="ClearShotsInFlightMessage" />). Then a disc grows from it across the
    ///     whole board, easing out over <see cref="INukeBlastSettings.Seconds" />. Each balloon and grid actor is nudged
    ///     outward just before the edge arrives (<see cref="INukeBlastSettings.NudgeLead" />). Each balloon the edge
    ///     reaches pops through <see cref="IBalloonPopper" />: its pop VFX play and its slot is freed, but no hit is
    ///     published, so nothing scores, no streak moves and no item fires. The other reward balloons and the grid
    ///     actors stay. When the time is up a last sweep pops anything left, a line that landed meanwhile included,
    ///     and the spawn clock gets the breather a big clear earns (<see cref="IBreatherGrant" />). A run reset, a level
    ///     change or game over stops the blast where it is, with no breather. It runs on scaled time, so a pause holds
    ///     it. The light, the push and the cloud are the views'.
    /// </summary>
    internal sealed class NukeBlast : INukeDetonator, IStartable, ITickable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly SlotGrid _grid;
        private readonly INukeBlastSettings _settings;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IPublisher<ClearShotsInFlightMessage> _shotsPublisher;
        private readonly IPublisher<NukeDetonatedMessage> _detonatedPublisher;
        private readonly IPublisher<NukeBreatherMessage> _breatherPublisher;
        private readonly IBalloonPopper _popper;
        private readonly IBreatherGrant _breather;
        private readonly INukeBlastView _view;
        private readonly INukeCloudView _cloud;
        private readonly HashSet<ISlotActor> _spared = new();
        private readonly HashSet<ISlotActor> _nudged = new();
        private readonly List<Vector2Int> _slots = new();
        private readonly CompositeDisposable _subscriptions = new();

        private bool _running;
        private Vector3 _center;
        private float _maxRadius;
        private float _elapsed;

        public int ResetOrder => RunResetOrder.Quiesce;

        internal bool IsRunning => _running;

        public NukeBlast(
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ILevelProgress levelProgress,
            SlotGrid grid,
            IBalloonsConfiguration balloons,
            IPublisher<NudgeMessage> nudgePublisher,
            IPublisher<ClearShotsInFlightMessage> shotsPublisher,
            IPublisher<NukeDetonatedMessage> detonatedPublisher,
            IPublisher<NukeBreatherMessage> breatherPublisher,
            IBalloonPopper popper,
            IBreatherGrant breather,
            INukeBlastView view,
            INukeCloudView cloud)
        {
            _gameOverSubscriber = gameOverSubscriber;
            _levelProgress = levelProgress;
            _grid = grid;
            _settings = balloons.NukeBlast;
            _nudgePublisher = nudgePublisher;
            _shotsPublisher = shotsPublisher;
            _detonatedPublisher = detonatedPublisher;
            _breatherPublisher = breatherPublisher;
            _popper = popper;
            _breather = breather;
            _view = view;
            _cloud = cloud;
        }

        public void Detonate(NukeBalloonModel nuke, Vector3 center)
        {
            // Only a cheat sets off a second nuke while a blast runs: the first finishes at once.
            if (_running)
            {
                Finish();
            }

            if (!_popper.TryPopSingle(nuke))
            {
                // It should always have a controller; the blast still goes, so the fuse's promise is kept.
                Log.Warn(nameof(NukeBlast), "No controller for the nuke that went off");
            }

            _shotsPublisher.Publish(new ClearShotsInFlightMessage());
            _detonatedPublisher.Publish(new NukeDetonatedMessage(center));

            _running = true;
            _center = center;
            _maxRadius = BoardDisc.FarthestReach(_grid, _center);
            _elapsed = 0f;
            _spared.Clear();
            _nudged.Clear();
            _view.Show(_center);
            _cloud.Play(_center);
        }

        public void Start()
        {
            _gameOverSubscriber.Subscribe(_ =>
            {
                Stop();
                _cloud.Stop();
            }).AddTo(_subscriptions);
            // Skip the value the property replays on subscribing: only a change of level stops the blast. The new
            // level shouldn't open under the last one's cloud either.
            _levelProgress.Level.Skip(1).Subscribe(_ =>
            {
                Stop();
                _cloud.Stop();
            }).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            Stop();
            _cloud.Stop();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Scaled time: a pause holds the blast.
        internal void Advance(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            _elapsed += deltaTime;
            var t = Mathf.Clamp01(_elapsed / _settings.Seconds);
            var radius = BoardDisc.RadiusAt(_maxRadius, t);
            _view.SetRadius(radius, t);
            NudgeAhead(radius + _settings.NudgeLead);
            Sweep(radius * radius);
            if (t >= 1f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Sweep(float.PositiveInfinity);
            Stop();
            _breather.Grant();
            _breatherPublisher.Publish(new NukeBreatherMessage(_center));
        }

        private void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _spared.Clear();
            _nudged.Clear();
            _view.Hide();
        }

        // Once each, as the edge comes within the lead: the board flinches outward before the edge arrives.
        private void NudgeAhead(float reach)
        {
            BoardDisc.SlotsWithin(_grid, _center, reach * reach, _nudged, _slots);
            foreach (var slot in _slots)
            {
                var actor = _grid.At(slot);
                _nudged.Add(actor);
                if (actor is IHasNudge nudgeable)
                {
                    _nudgePublisher.Publish(new NudgeMessage(nudgeable, _center, NudgeType.Wave, _settings.NudgeOverrides));
                }
            }
        }

        private void Sweep(float radiusSqr)
        {
            BoardDisc.SlotsWithin(_grid, _center, radiusSqr, _spared, _slots);
            foreach (var slot in _slots)
            {
                Reach(_grid.At(slot));
            }
        }

        // A balloon that isn't a reward pops; the other rewards and the grid actors stay.
        private void Reach(ISlotActor actor)
        {
            if (actor is not IBalloonModel balloon || balloon.TypeName.IsReward())
            {
                _spared.Add(actor);
                return;
            }

            if (!_popper.TryPopSingle(balloon))
            {
                // Every balloon on the grid should have a controller. Skip one that doesn't, so the sweeps don't retry
                // it every frame.
                _spared.Add(actor);
                Log.Warn(nameof(NukeBlast), $"No controller for the {balloon.TypeName} balloon in the blast's way");
            }
        }
    }
}
