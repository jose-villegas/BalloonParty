using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Item.Paint;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     After a disco ball's pop, the next few clock lines arrive rainbow (PLAN-DiscoBalloon), so the party outlasts
    ///     the wave. <see cref="DiscoWave" /> grants the lines; each line that brings a balloon pays one, by the wave's
    ///     rules (<see cref="PaintTargets" />): a paintable balloon turns rainbow, and one immune to paint keeps its
    ///     look. The painted balloons are booked to the shot that broke the ball (<see cref="ShotPaintLedger" />) while it
    ///     flies, as the wave's are. A level change or a run reset forgets the lines still owed.
    /// </summary>
    internal sealed class RainbowLines : IRainbowLines, IStartable, IRunResettable, IDisposable
    {
        private readonly IGamePalette _palette;
        private readonly ShotPaintLedger _paintLedger;
        private readonly IProjectileRegistry _projectiles;
        private readonly ILevelProgress _levelProgress;
        private readonly CompositeDisposable _subscriptions = new();

        private int _owed;
        private IProjectileModel _shot;

        public int ResetOrder => RunResetOrder.Quiesce;

        internal int Owed => _owed;

        public RainbowLines(
            IGamePalette palette, ShotPaintLedger paintLedger, IProjectileRegistry projectiles,
            ILevelProgress levelProgress)
        {
            _palette = palette;
            _paintLedger = paintLedger;
            _projectiles = projectiles;
            _levelProgress = levelProgress;
        }

        public void Start()
        {
            // Skip the value the property replays on subscribing: only a change of level forgets the lines.
            _levelProgress.Level.Skip(1).Subscribe(_ => Forget()).AddTo(_subscriptions);
            // A retired model can come back as a later shot, which mustn't inherit this one's booking.
            _projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
        }

        public void ResetRun(int generation)
        {
            Forget();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // A clock line that placed nothing (a full board) leaves the debt as it was, so the lines owed all arrive.
        public void PaintLine(IReadOnlyList<IBalloonModel> spawned, int from)
        {
            if (_owed <= 0 || from >= spawned.Count)
            {
                return;
            }

            _owed--;
            for (var i = from; i < spawned.Count; i++)
            {
                var balloon = spawned[i];
                if (PaintTargets.Classify(balloon, GamePalette.RainbowColorId, _palette, out var paintable)
                    != PaintVerdict.Accept)
                {
                    continue;
                }

                paintable.Color.Value = GamePalette.RainbowColorId;
                _paintLedger.Record(_shot, balloon);
            }
        }

        // A second disco ball's pop while lines are still owed tops them up rather than stacking them.
        internal void Grant(int lines, IProjectileModel shot)
        {
            _owed = Math.Max(_owed, lines);
            _shot = shot;
        }

        private void OnRetired(IProjectileModel shot)
        {
            if (ReferenceEquals(shot, _shot))
            {
                _shot = null;
            }
        }

        private void Forget()
        {
            _owed = 0;
            _shot = null;
        }
    }
}
