using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Projectile.Model;
using UniRx;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Which balloons each shot painted rainbow itself (the disco ball's wave, PLAN-DiscoBalloon). The shot that
    ///     breaks a disco ball is still flying, and a rainbow pop climbs its streak whatever the colour, so without
    ///     this it could paint the board and then climb on everything it painted. A pop of a balloon its own shot
    ///     painted scores at the streak as it stands and leaves it there (<c>DamageFlags.OwnPaint</c>); another
    ///     shot's pop of it climbs as any rainbow pop does. A shot's entries go when it retires.
    /// </summary>
    internal sealed class ShotPaintLedger : IDisposable
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly Dictionary<IProjectileModel, HashSet<IBalloonModel>> _byShot = new();
        private readonly Stack<HashSet<IBalloonModel>> _spare = new();
        private readonly IDisposable _retiredSubscription;

        internal ShotPaintLedger(IProjectileRegistry projectiles)
        {
            _projectiles = projectiles;
            _retiredSubscription = projectiles.Retired.Subscribe(OnRetired);
        }

        public void Dispose()
        {
            _retiredSubscription.Dispose();
        }

        // A shot already retired never retires again, so an entry for it would never be dropped.
        internal void Record(IProjectileModel shot, IBalloonModel balloon)
        {
            if (shot == null || balloon == null || !_projectiles.IsTracked(shot))
            {
                return;
            }

            if (!_byShot.TryGetValue(shot, out var painted))
            {
                painted = _spare.Count > 0 ? _spare.Pop() : new HashSet<IBalloonModel>();
                _byShot.Add(shot, painted);
            }

            painted.Add(balloon);
        }

        internal bool Painted(IProjectileModel shot, IBalloonModel balloon)
        {
            return shot != null && _byShot.TryGetValue(shot, out var painted) && painted.Contains(balloon);
        }

        private void OnRetired(IProjectileModel shot)
        {
            if (!_byShot.Remove(shot, out var painted))
            {
                return;
            }

            painted.Clear();
            _spare.Push(painted);
        }
    }
}
