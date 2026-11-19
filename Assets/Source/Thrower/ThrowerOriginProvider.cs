using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     Where a shot leaves from, readable outside the thrower's own scope.
    /// </summary>
    /// <remarks>
    ///     <see cref="ThrowerView" /> lives in <c>ThrowerLifetimeScope</c>, a child of the game scope,
    ///     so systems registered above it — <c>ItemAssigner</c>, which needs a launch point to plan a
    ///     shield chain from — cannot inject it. So it is a plain game-scope singleton the thrower fills in.
    /// </remarks>
    internal sealed class ThrowerOriginProvider : IShotOrigin
    {
        private Transform _spawnPoint;
        private Transform _pivot;
        private Vector2 _restingPivot;
        private float _projectileContactRadius;

        /// <summary>False until the thrower has started; callers must plan without an origin.</summary>
        internal bool IsAvailable => _spawnPoint != null;

        /// <summary>
        ///     The spawn point, not the pivot. It orbits the pivot with the aim, so this is a snapshot
        ///     of one aim state rather than a fixed launch point — close enough for planning, which
        ///     samples a fan of angles anyway and cannot know which the player will pick.
        /// </summary>
        internal Vector2 Origin => _spawnPoint != null ? (Vector2)_spawnPoint.position : Vector2.zero;

        /// <summary>Where the cannon turns about: unlike <see cref="Origin" />, it stays put whatever the aim.
        /// The cannon as it stands right now, mid entrance or mid recoil included.</summary>
        internal Vector2 Pivot => _pivot != null ? (Vector2)_pivot.position : Origin;

        /// <summary>
        ///     Where the cannon turns about once it has entered and is not recoiling: the point given to
        ///     <see cref="Set" />, which does not move with the cannon. What a board laid out for the whole level is
        ///     planned from (the meadow's mushrooms).
        /// </summary>
        internal Vector2 RestingPivot => _restingPivot;

        /// <summary>
        ///     The shot's own contact circle, so a planner can inflate its targets the way the real
        ///     deflection does. Zero until the thrower starts.
        /// </summary>
        internal float ProjectileContactRadius => _projectileContactRadius;

        // The board's side (the mushrooms' circles and their placement) asks through the interface, so it can
        // be built and tested without a thrower.
        bool IShotOrigin.IsAvailable => IsAvailable;
        Vector2 IShotOrigin.RestingPivot => RestingPivot;
        float IShotOrigin.ProjectileContactRadius => ProjectileContactRadius;

        /// <param name="restingPivot">Where <paramref name="pivot" /> will be once the cannon has entered: its
        /// position now plus the offset its entrance moves it by. Kept as given.</param>
        internal void Set(Transform spawnPoint, Transform pivot, float projectileContactRadius, Vector2 restingPivot)
        {
            _spawnPoint = spawnPoint;
            _pivot = pivot;
            _projectileContactRadius = projectileContactRadius;
            _restingPivot = restingPivot;
        }

        internal void Clear()
        {
            _spawnPoint = null;
            _pivot = null;
            _projectileContactRadius = 0f;
            _restingPivot = Vector2.zero;
        }
    }
}
