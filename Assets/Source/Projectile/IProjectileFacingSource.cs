using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     Read-only facing surface for the loaded/flying shot, exposed to a pooled (non-injected) item
    ///     visual — see <see cref="ProjectileFacingSource"/> for the DI-side implementation.
    /// </summary>
    internal interface IProjectileFacingSource
    {
        bool IsFlying { get; }
        bool IsAiming { get; }
        Vector3 ProjectilePosition { get; }

        // The live aim direction while a shot is loaded, and the flight direction once fired — the same
        // IProjectileModel field across both phases, kept in sync by ThrowerController.
        Vector2 Direction { get; }
        IReadOnlyList<Vector3> PredictionPoints { get; }
        int PredictionVersion { get; }
    }
}
