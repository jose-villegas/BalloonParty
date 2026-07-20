using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    internal enum ProjectileStepOutcome
    {
        Moved,
        Bounced,
        Destroyed
    }

    /// <summary>Result of one fixed-step advance by <see cref="ProjectileMotionResolver" />.
    /// On a bounce, <see cref="Position" /> is the mirror-reflected continuation (the exact billiard
    /// point, up to a step INSIDE the field) while <see cref="WallContact" /> is the wall-projected
    /// point the bounce visually happened at — VFX and shield-loss feedback belong there.</summary>
    internal readonly struct ProjectileStep
    {
        public readonly Vector3 Position;
        public readonly Vector3 WallContact;
        public readonly Vector2 Direction;

        /// <summary>The resolved flight speed used to take this step (post-buff, post-cruise-ramp) — the
        /// instantaneous velocity magnitude for velocity-scaled feedback (e.g. the shield-loss stamp).</summary>
        public readonly float Speed;

        public readonly ProjectileStepOutcome Outcome;

        private ProjectileStep(
            Vector3 position, Vector3 wallContact, Vector2 direction, float speed, ProjectileStepOutcome outcome)
        {
            Position = position;
            WallContact = wallContact;
            Direction = direction;
            Speed = speed;
            Outcome = outcome;
        }

        internal static ProjectileStep Moved(Vector3 position, Vector2 direction, float speed)
        {
            return new ProjectileStep(position, position, direction, speed, ProjectileStepOutcome.Moved);
        }

        internal static ProjectileStep Bounced(Vector3 position, Vector3 wallContact, Vector2 direction, float speed)
        {
            return new ProjectileStep(position, wallContact, direction, speed, ProjectileStepOutcome.Bounced);
        }

        internal static ProjectileStep Destroyed(Vector3 position, Vector2 direction, float speed)
        {
            return new ProjectileStep(position, position, direction, speed, ProjectileStepOutcome.Destroyed);
        }
    }
}
