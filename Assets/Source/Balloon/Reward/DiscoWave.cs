using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Item.Paint;
using BalloonParty.Nudge;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
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
    ///     The disco ball's pop (PLAN-DiscoBalloon): a disc grows from where it popped across the whole board, easing
    ///     out over <see cref="IDiscoWaveSettings.Seconds" />. Each balloon its edge reaches is nudged away from the
    ///     pop once; a paintable one of a concrete colour bursts in that colour and turns rainbow, and one immune to
    ///     paint (<see cref="PaintTargets" />) takes the drip and keeps its colour. Balloons that move while the disc
    ///     grows are caught by a last sweep when it ends. What the wave paints is booked to the breaking shot
    ///     (<see cref="ShotPaintLedger" />), so that shot can't climb its streak on it. A run reset, a level change or
    ///     game over stops the wave where it is. The wave's sound follows its messages: how far through its time it is
    ///     (<see cref="DiscoWaveAdvancedMessage" />), and each balloon it paints (<see cref="DiscoWaveReachedMessage" />).
    /// </summary>
    internal sealed class DiscoWave : IStartable, ITickable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly SlotGrid _grid;
        private readonly IGamePalette _palette;
        private readonly IDiscoWaveSettings _settings;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IPublisher<DiscoWaveAdvancedMessage> _advancedPublisher;
        private readonly IPublisher<DiscoWaveReachedMessage> _reachedPublisher;
        private readonly ShotPaintLedger _paintLedger;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly IDiscoWaveView _view;
        private readonly HashSet<ISlotActor> _reached = new();
        private readonly CompositeDisposable _subscriptions = new();

        private bool _running;
        private Vector3 _center;
        private float _maxRadius;
        private float _elapsed;
        private IProjectileModel _shot;
        // A ball's pop can be published twice (see RewardBalloonPayout); it starts one wave.
        private IBalloonModel _lastSource;

        public int ResetOrder => RunResetOrder.Quiesce;

        internal bool IsRunning => _running;

        public DiscoWave(
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ILevelProgress levelProgress,
            SlotGrid grid,
            IGamePalette palette,
            IBalloonsConfiguration balloons,
            IPublisher<NudgeMessage> nudgePublisher,
            IPublisher<DiscoWaveAdvancedMessage> advancedPublisher,
            IPublisher<DiscoWaveReachedMessage> reachedPublisher,
            ShotPaintLedger paintLedger,
            DisturbanceFieldService disturbanceField,
            IDiscoWaveView view)
        {
            _hitSubscriber = hitSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _levelProgress = levelProgress;
            _grid = grid;
            _palette = palette;
            _settings = balloons.DiscoWave;
            _nudgePublisher = nudgePublisher;
            _advancedPublisher = advancedPublisher;
            _reachedPublisher = reachedPublisher;
            _paintLedger = paintLedger;
            _disturbanceField = disturbanceField;
            _view = view;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnHit).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(_ => Stop()).AddTo(_subscriptions);
            // Skip the value the property replays on subscribing: only a change of level stops the wave.
            _levelProgress.Level.Skip(1).Subscribe(_ => Stop()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            Stop();
            _lastSource = null;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        internal void OnHit(ActorHitMessage msg)
        {
            if (!RewardBalloonPop.TryGet(msg, out var balloon) || balloon.TypeName != BalloonType.Disco
                || ReferenceEquals(balloon, _lastSource))
            {
                return;
            }

            // A second disco ball popped before the first wave ended: the first finishes at once.
            if (_running)
            {
                Finish();
            }

            _lastSource = balloon;
            _running = true;
            _center = msg.WorldPosition;
            _maxRadius = FarthestReach(_center);
            _elapsed = 0f;
            _shot = msg.Context.Projectile;
            _reached.Clear();
            _reached.Add(balloon);
            _view.Show(_center);
            _advancedPublisher.Publish(new DiscoWaveAdvancedMessage(0f));
        }

        // Scaled time: a pause holds the wave.
        internal void Advance(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            _elapsed += deltaTime;
            var t = Mathf.Clamp01(_elapsed / _settings.Seconds);
            var radius = _maxRadius * t * (2f - t);
            _view.SetRadius(radius, t);
            _advancedPublisher.Publish(new DiscoWaveAdvancedMessage(t));
            Sweep(radius * radius);
            if (t >= 1f)
            {
                Finish();
            }
        }

        // The disc ends past the farthest slot by a balloon's radius, so its edge clears the last balloon.
        private float FarthestReach(Vector3 center)
        {
            var farthestSqr = 0f;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var offset = (Vector2)(_grid.IndexToWorldPosition(new Vector2Int(col, row)) - center);
                    farthestSqr = Mathf.Max(farthestSqr, offset.sqrMagnitude);
                }
            }

            var spacing = _grid.IndexToWorldPosition(new Vector2Int(1, 0)) - _grid.IndexToWorldPosition(Vector2Int.zero);
            return Mathf.Sqrt(farthestSqr) + spacing.magnitude * 0.5f;
        }

        private void Finish()
        {
            Sweep(float.PositiveInfinity);
            Stop();
        }

        private void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _shot = null;
            _reached.Clear();
            _view.Hide();
        }

        private void Sweep(float radiusSqr)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    var actor = _grid.At(slot);
                    if (_reached.Contains(actor)
                        || ((Vector2)(_grid.IndexToWorldPosition(slot) - _center)).sqrMagnitude > radiusSqr)
                    {
                        continue;
                    }

                    _reached.Add(actor);
                    Reach(slot, actor);
                }
            }
        }

        private void Reach(Vector2Int slot, ISlotActor actor)
        {
            if (actor is IHasNudge nudgeable)
            {
                _nudgePublisher.Publish(new NudgeMessage(nudgeable, _center, NudgeType.Wave, _settings.NudgeOverrides));
            }

            var verdict = PaintTargets.Classify(actor, GamePalette.RainbowColorId, _palette, out var paintable);
            if (verdict == PaintVerdict.Ignore)
            {
                return;
            }

            var view = _grid.ActorViewAt<IPaintReactive>(slot);
            var position = _grid.IndexToWorldPosition(slot);
            if (verdict == PaintVerdict.Accept)
            {
                var oldColorId = paintable.Color.Value;
                paintable.Color.Value = GamePalette.RainbowColorId;
                view?.PlayRecolourBurst(_palette.GetColor(oldColorId), _settings.BurstScale);
                // Tagged with the colour that bursts off, as the Paint item tags a blob with its own.
                _disturbanceField.Stamp(
                    StampSource.Paint, position, ((Vector2)(position - _center)).normalized,
                    paletteIndex: _palette.PaletteIndexOf(oldColorId));
                // The ledger is what keeps the breaking shot off its own paint; a paintable it can't book escapes it.
                Log.Assert(paintable is IBalloonModel, "DiscoWave", $"Painted a non-balloon {paintable}: the shot isn't kept off it.");
                if (paintable is IBalloonModel painted)
                {
                    _paintLedger.Record(_shot, painted);
                }
            }

            // The palette gives the rainbow id white, so the flag is what makes the drip draw the level's bands.
            view?.PlayPaintDrip(_palette.GetColor(GamePalette.RainbowColorId), rainbow: true);
            var reach = _maxRadius > 0f ? ((Vector2)(position - _center)).magnitude / _maxRadius : 0f;
            _reachedPublisher.Publish(new DiscoWaveReachedMessage(position, reach));
        }
    }
}
