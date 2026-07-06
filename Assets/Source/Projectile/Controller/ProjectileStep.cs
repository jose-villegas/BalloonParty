using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    internal enum ProjectileStepOutcome
    {
        Moved,
        Bounced,
        Destroyed
    }

    /// <summary>Result of one fixed-step advance by <see cref="ProjectileMotionResolver" />.</summary>
    internal readonly struct ProjectileStep
    {
        public readonly Vector3 Position;
        public readonly Vector2 Direction;
        public readonly ProjectileStepOutcome Outcome;

        private ProjectileStep(Vector3 position, Vector2 direction, ProjectileStepOutcome outcome)
        {
            Position = position;
            Direction = direction;
            Outcome = outcome;
        }

        internal static ProjectileStep Moved(Vector3 position, Vector2 direction)
        {
            return new ProjectileStep(position, direction, ProjectileStepOutcome.Moved);
        }

        internal static ProjectileStep Bounced(Vector3 position, Vector2 direction)
        {
            return new ProjectileStep(position, direction, ProjectileStepOutcome.Bounced);
        }

        internal static ProjectileStep Destroyed(Vector3 position, Vector2 direction)
        {
            return new ProjectileStep(position, direction, ProjectileStepOutcome.Destroyed);
        }
    }
}
