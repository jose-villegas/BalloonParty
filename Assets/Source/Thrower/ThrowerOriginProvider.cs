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
        private float _projectileContactRadius;

        /// <summary>False until the thrower has started; callers must plan without an origin.</summary>
        internal bool IsAvailable => _spawnPoint != null;

        /// <summary>
        ///     The spawn point, not the pivot. It orbits the pivot with the aim, so this is a snapshot
        ///     of one aim state rather than a fixed launch point — close enough for planning, which
        ///     samples a fan of angles anyway and cannot know which the player will pick.
        /// </summary>
        internal Vector2 Origin => _spawnPoint != null ? (Vector2)_spawnPoint.position : Vector2.zero;

        /// <summary>
        ///     The shot's own contact circle, so a planner can inflate its targets the way the real
        ///     deflection does. Zero until the thrower starts.
        /// </summary>
        internal float ProjectileContactRadius => _projectileContactRadius;

        internal void Set(Transform spawnPoint, float projectileContactRadius)
        {
            _spawnPoint = spawnPoint;
            _projectileContactRadius = projectileContactRadius;
        }

        internal void Clear()
        {
            _spawnPoint = null;
            _projectileContactRadius = 0f;
        }
    }
}
