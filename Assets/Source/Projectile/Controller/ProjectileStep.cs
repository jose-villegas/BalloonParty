using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    internal enum ProjectileStepOutcome
    {
        Moved,
        Bounced,
        Destroyed,

        // The ball bounced off a bumper (a meadow mushroom) and met no wall on the step. No shield is spent.
        BumperBounced
    }

    /// <summary>Result of one fixed-step advance by <see cref="ProjectileMotionResolver" />.
    /// On a bounce, <see cref="Position" /> is the mirror-reflected continuation (the exact billiard
    /// point, up to a step INSIDE the field) while <see cref="WallContact" /> is the wall-projected
    /// point the bounce visually happened at — VFX and shield-loss feedback belong there.</summary>
    internal readonly struct ProjectileStep
    {
        public readonly Vector3 Position;

        /// <summary>The wall-projected point of a wall bounce. On a
        /// <see cref="ProjectileStepOutcome.BumperBounced" /> step, where the ball's centre touched the last
        /// bumper it bounced off.</summary>
        public readonly Vector3 WallContact;

        public readonly Vector2 Direction;

        /// <summary>
        ///     The direction the ball left the wall along, on a step that bounced off one. It is
        ///     <see cref="Direction" /> unless a bumper on the way back off the wall turned the ball again within the
        ///     step: what is said of the wall bounce itself (the shield's squash, the wake's turn) wants this one,
        ///     and where the ball goes next wants <see cref="Direction" />. On any other step, <see cref="Direction" />.
        /// </summary>
        public readonly Vector2 WallDirection;

        /// <summary>The resolved flight speed used to take this step (post-buff, post-cruise-ramp) — the
        /// instantaneous velocity magnitude for velocity-scaled feedback (e.g. the shield-loss stamp).</summary>
        public readonly float Speed;

        public readonly ProjectileStepOutcome Outcome;

        /// <summary>
        ///     How many bumpers the ball bounced off during the step, whatever its <see cref="Outcome" />: a
        ///     step can bounce off a bumper and still reach a wall. Each one is in
        ///     <see cref="ProjectileMotionResolver.StepBumperBounces" />. Always 0 on a board without bumpers.
        /// </summary>
        public readonly int BumperBounces;

        private ProjectileStep(
            Vector3 position, Vector3 wallContact, (Vector2 Final, Vector2 OffTheWall) direction, float speed,
            ProjectileStepOutcome outcome, int bumperBounces = 0)
        {
            Position = position;
            WallContact = wallContact;
            Direction = direction.Final;
            WallDirection = direction.OffTheWall;
            Speed = speed;
            Outcome = outcome;
            BumperBounces = bumperBounces;
        }

        internal static ProjectileStep Moved(Vector3 position, Vector2 direction, float speed)
        {
            return new ProjectileStep(position, position, (direction, direction), speed, ProjectileStepOutcome.Moved);
        }

        internal static ProjectileStep Bounced(Vector3 position, Vector3 wallContact, Vector2 direction, float speed)
        {
            return new ProjectileStep(
                position, wallContact, (direction, direction), speed, ProjectileStepOutcome.Bounced);
        }

        internal static ProjectileStep Destroyed(Vector3 position, Vector2 direction, float speed)
        {
            return new ProjectileStep(
                position, position, (direction, direction), speed, ProjectileStepOutcome.Destroyed);
        }

        /// <summary>
        ///     This step with the <paramref name="bounces" /> bumpers it bounced off counted in. A step that only
        ///     moved becomes <see cref="ProjectileStepOutcome.BumperBounced" />, reporting
        ///     <paramref name="lastContact" />; one that reached a wall keeps its outcome and its wall contact.
        /// </summary>
        internal ProjectileStep WithBumperBounces(int bounces, Vector3 lastContact)
        {
            if (bounces <= 0)
            {
                return this;
            }

            return Outcome == ProjectileStepOutcome.Moved
                ? new ProjectileStep(
                    Position, lastContact, (Direction, WallDirection), Speed, ProjectileStepOutcome.BumperBounced,
                    bounces)
                : new ProjectileStep(Position, WallContact, (Direction, WallDirection), Speed, Outcome, bounces);
        }

        /// <summary>This step ending at <paramref name="position" /> heading <paramref name="direction" />
        /// instead: a wall bounce whose way back off the wall a bumper turned. The wall's own direction is kept
        /// as <see cref="WallDirection" />.</summary>
        internal ProjectileStep Rerouted(Vector3 position, Vector2 direction)
        {
            return new ProjectileStep(position, WallContact, (direction, WallDirection), Speed, Outcome, BumperBounces);
        }
    }
}
