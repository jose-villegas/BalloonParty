using UnityEngine;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     Where a shot leaves from, readable outside the thrower's own scope.
    /// </summary>
    /// <remarks>
    ///     <see cref="ThrowerView" /> lives in <c>ThrowerLifetimeScope</c>, a child of the game scope,
    ///     so systems registered above it — <c>ItemAssigner</c>, which needs a launch point to plan a
    ///     shield chain from — cannot inject it. Same shape as
    ///     <c>ProjectilePositionProvider</c>: a plain game-scope singleton the thrower fills in.
    /// </remarks>
    internal sealed class ThrowerOriginProvider
    {
        private Transform _spawnPoint;

        /// <summary>False until the thrower has started; callers must plan without an origin.</summary>
        internal bool IsAvailable => _spawnPoint != null;

        /// <summary>
        ///     The spawn point, not the pivot. It orbits the pivot with the aim, so this is a snapshot
        ///     of one aim state rather than a fixed launch point — close enough for planning, which
        ///     samples a fan of angles anyway and cannot know which the player will pick.
        /// </summary>
        internal Vector2 Origin => _spawnPoint != null ? (Vector2)_spawnPoint.position : Vector2.zero;

        internal void Set(Transform spawnPoint)
        {
            _spawnPoint = spawnPoint;
        }

        internal void Clear()
        {
            _spawnPoint = null;
        }
    }
}
