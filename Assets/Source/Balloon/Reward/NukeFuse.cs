using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Nudge;
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
    ///     The armed nuke's fuse (PLAN-NukeBalloon). The hit that arms a nuke (any <see cref="ActorHitMessage" /> on a
    ///     <see cref="NukeBalloonModel" /> that is now <see cref="NukeBalloonModel.Armed" />) starts it. It counts
    ///     <see cref="INukeFuseSettings.Seconds" /> down on scaled time, so a pause holds it, and shows the whole seconds
    ///     left on the nuke's counter, with a <see cref="NukeCountdownMessage" /> for each. Meanwhile it pulls the
    ///     balloons around the nuke in towards it, harder as the count runs down, and the view implodes the
    ///     disturbance field at it. At zero <see cref="INukeDetonator" /> sets it off. A level change, a game over, a
    ///     run reset or the nuke leaving the board stops the fuse with no blast.
    /// </summary>
    internal sealed class NukeFuse : IStartable, ITickable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly SlotGrid _grid;
        private readonly INukeFuseSettings _settings;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IPublisher<NukeArmedMessage> _armedPublisher;
        private readonly IPublisher<NukeCountdownMessage> _countdownPublisher;
        private readonly INukeDetonator _detonator;
        private readonly INukeBlastView _view;
        private readonly List<Vector2Int> _slots = new();
        private readonly HashSet<ISlotActor> _none = new();
        private readonly NudgeOverride[] _pull = { new(NudgeType.Wave, 0f) };
        private readonly CompositeDisposable _subscriptions = new();

        private NukeBalloonModel _nuke;
        private Vector2Int _slot;
        private Vector3 _center;
        private float _elapsed;
        private float _sincePull;

        public int ResetOrder => RunResetOrder.Quiesce;

        internal bool IsRunning => _nuke != null;

        public NukeFuse(
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ILevelProgress levelProgress,
            SlotGrid grid,
            IBalloonsConfiguration balloons,
            IPublisher<NudgeMessage> nudgePublisher,
            IPublisher<NukeArmedMessage> armedPublisher,
            IPublisher<NukeCountdownMessage> countdownPublisher,
            INukeDetonator detonator,
            INukeBlastView view)
        {
            _hitSubscriber = hitSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _levelProgress = levelProgress;
            _grid = grid;
            _settings = balloons.NukeFuse;
            _nudgePublisher = nudgePublisher;
            _armedPublisher = armedPublisher;
            _countdownPublisher = countdownPublisher;
            _detonator = detonator;
            _view = view;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnHit).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(_ => Stop()).AddTo(_subscriptions);
            // Skip the value the property replays on subscribing: only a change of level stops the fuse.
            _levelProgress.Level.Skip(1).Subscribe(_ => Stop()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            Stop();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        internal void OnHit(ActorHitMessage msg)
        {
            if (msg.Actor is not NukeBalloonModel nuke || !nuke.Armed.Value || ReferenceEquals(nuke, _nuke))
            {
                return;
            }

            // Only a cheat arms a second nuke while a fuse runs: the first goes off now.
            if (_nuke != null)
            {
                GoOff();
            }

            _nuke = nuke;
            _slot = nuke.SlotIndex.Value;
            _center = _grid.IndexToWorldPosition(_slot);
            _elapsed = 0f;
            _sincePull = 0f;
            _armedPublisher.Publish(new NukeArmedMessage(_center));
            _view.Implode(_center, _settings.Seconds);
            ShowSecondsLeft();
            Pull();
        }

        // Scaled time: a pause holds the fuse.
        internal void Advance(float deltaTime)
        {
            if (_nuke == null)
            {
                return;
            }

            // The board took it (a level-up's clear, the game over's pop wave): nothing to set off. Anchored, it
            // shouldn't move, but if something does move it the fuse follows it rather than leave it armed for good.
            var slot = _nuke.SlotIndex.Value;
            if (!ReferenceEquals(_grid.At(slot), _nuke))
            {
                Stop();
                return;
            }

            if (slot != _slot)
            {
                _slot = slot;
                _center = _grid.IndexToWorldPosition(slot);
            }

            _elapsed += deltaTime;
            if (_elapsed >= _settings.Seconds)
            {
                GoOff();
                return;
            }

            ShowSecondsLeft();
            _sincePull += deltaTime;
            if (_sincePull >= _settings.PullInterval)
            {
                _sincePull -= _settings.PullInterval;
                Pull();
            }
        }

        private void ShowSecondsLeft()
        {
            var secondsLeft = Mathf.CeilToInt(_settings.Seconds - _elapsed - 1e-4f);
            if (secondsLeft == _nuke.FuseSecondsLeft.Value)
            {
                return;
            }

            _nuke.SetFuseSecondsLeft(secondsLeft);
            _countdownPublisher.Publish(new NukeCountdownMessage(_center, secondsLeft));
        }

        // Every balloon near the nuke is drawn towards it: a nudge with a negative distance pulls instead of pushing.
        private void Pull()
        {
            var t = Mathf.Clamp01(_elapsed / _settings.Seconds);
            var distance = Mathf.Lerp(_settings.PullDistanceStart, _settings.PullDistanceEnd, t);
            _pull[0] = new NudgeOverride(NudgeType.Wave, -distance, _settings.PullSeconds);
            BoardDisc.SlotsWithin(_grid, _center, _settings.PullRadius * _settings.PullRadius, _none, _slots);
            foreach (var slot in _slots)
            {
                if (_grid.At(slot) is IHasNudge nudgeable && !ReferenceEquals(nudgeable, _nuke))
                {
                    _nudgePublisher.Publish(new NudgeMessage(nudgeable, _center, NudgeType.Wave, _pull));
                }
            }
        }

        private void GoOff()
        {
            var nuke = _nuke;
            var center = _center;
            Stop();
            nuke.SetFuseSecondsLeft(0);
            _detonator.Detonate(nuke, center);
        }

        private void Stop()
        {
            _nuke = null;
        }
    }
}
