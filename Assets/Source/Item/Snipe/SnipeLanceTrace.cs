using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Where each Snipe-armed shot's current straight run began: the point it armed at, then each wall
    ///     it carries the lance past. Pure bookkeeping, so the pencil line's segmenting is tested headless.
    /// </summary>
    /// <remarks>
    ///     Assumes a piercing shot only turns at walls: every balloon a pierce meets pops or is plowed, none
    ///     deflects it. A deflector that bent a piercing shot mid-run would need to close the run there too.
    /// </remarks>
    internal sealed class SnipeLanceTrace
    {
        // Shorter runs than this draw nothing — a banked charge re-arming on a cruise pierce opens at the
        // very wall the run then closes on.
        private const float MinRunLength = 1e-3f;

        private readonly Dictionary<IProjectileModel, Vector3> _runStarts = new();
        private readonly HashSet<IProjectileModel> _armedBeforeLaunch = new();

        internal int OpenCount => _runStarts.Count;

        internal bool IsOpen(IProjectileModel shot)
        {
            return shot != null && _runStarts.ContainsKey(shot);
        }

        /// <summary>
        ///     Starts a run at <paramref name="start" />. Ignored while one is already open: a charge banked
        ///     on a running lance re-arms it without the shot ever leaving its path.
        /// </summary>
        internal void Open(IProjectileModel shot, Vector3 start)
        {
            if (shot == null || _runStarts.ContainsKey(shot))
            {
                return;
            }

            _runStarts.Add(shot, start);
        }

        /// <summary>
        ///     A lance armed on a shot still in the thrower: its first run starts at the muzzle, once
        ///     <see cref="Launched" /> says where that is.
        /// </summary>
        internal void OpenAtLaunch(IProjectileModel shot)
        {
            if (shot != null && !_runStarts.ContainsKey(shot))
            {
                _armedBeforeLaunch.Add(shot);
            }
        }

        internal void Launched(IProjectileModel shot, Vector3 muzzle)
        {
            if (shot != null && _armedBeforeLaunch.Remove(shot))
            {
                Open(shot, muzzle);
            }
        }

        /// <summary>
        ///     Ends the open run at <paramref name="wall" />, reporting where it began. A lance still armed
        ///     past the wall starts its next run there; otherwise the shot stops being tracked. False when no
        ///     run was open, or the run is too short to draw.
        /// </summary>
        internal bool TryCloseRun(IProjectileModel shot, Vector3 wall, bool stillPiercing, out Vector3 from)
        {
            from = default;
            if (shot == null || !_runStarts.TryGetValue(shot, out var start))
            {
                return false;
            }

            if (stillPiercing)
            {
                _runStarts[shot] = wall;
            }
            else
            {
                _runStarts.Remove(shot);
            }

            from = start;
            return (wall - start).sqrMagnitude >= MinRunLength * MinRunLength;
        }

        internal void Drop(IProjectileModel shot)
        {
            if (shot != null)
            {
                _runStarts.Remove(shot);
                _armedBeforeLaunch.Remove(shot);
            }
        }

        internal void Clear()
        {
            _runStarts.Clear();
            _armedBeforeLaunch.Clear();
        }
    }
}
