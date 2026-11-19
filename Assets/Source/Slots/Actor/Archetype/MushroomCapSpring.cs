using BalloonParty.Shared.Math;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     The motion of a mushroom's cap after a hit (@ref plan_meadow_mushrooms): a spring that is pushed along
    ///     the ball's way in, so the cap leans away from the hit and wobbles back along that same line, and a
    ///     second one that swells the cap when the bounce pays the lance. Pure: <see cref="MushroomView" /> steps
    ///     it and draws what it holds.
    /// </summary>
    internal struct MushroomCapSpring
    {
        // The spring is stepped no coarser than this, so a slow frame wobbles as a fast one does and the
        // integration stays stable at any tuned frequency.
        private const float MaxStepSeconds = 1f / 120f;

        // A frame longer than these steps cover (a hitch) loses the time over it; the cap just wobbles on late.
        private const int MaxStepsPerFrame = 8;

        // The swell at rest has no unit to measure against but its own full size.
        private const float RestPulse = 0.002f;

        private DampedSpring2D _lean;
        private DampedSpring1D _pulse;

        /// <summary>Where the cap stands off its rest, in world units.</summary>
        public Vector2 Lean => _lean.Position;

        /// <summary>How swollen the cap is: 0 at rest, about the tuned pulse at its fullest, and a little under 0
        /// as it wobbles back.</summary>
        public float Pulse => _pulse.Position;

        /// <summary>True when the cap neither leans nor swells nor moves: nothing to step, nothing to draw.</summary>
        public bool IsAtRest =>
            _lean.Position == Vector2.zero && _lean.Velocity == Vector2.zero
            && _pulse.Position == 0f && _pulse.Velocity == 0f;

        /// <summary>
        ///     A ball arriving along <paramref name="incoming" /> at <paramref name="speed" /> world units a second
        ///     pushes the cap that way. A faster ball leans it further, up to the tuned most.
        /// </summary>
        public void Hit(Vector2 incoming, float speed, in MushroomLeanTuning tuning)
        {
            var length = incoming.magnitude;
            if (length < 1e-5f)
            {
                return;
            }

            var reach = Mathf.Min(Mathf.Max(0f, speed) * tuning.LeanPerSpeed, tuning.MaxLean);
            _lean.AddImpulse(incoming / length * (reach * tuning.KickPerReach));
        }

        /// <summary>
        ///     A blast pushes the cap along <paramref name="away" />, as hard as <paramref name="strength" /> (0 to
        ///     1) of the tuned sway: it leans that way and swings back toward the blast, as after a ball's hit.
        /// </summary>
        public void Sway(Vector2 away, float strength, in MushroomLeanTuning tuning)
        {
            var length = away.magnitude;
            if (length < 1e-5f)
            {
                return;
            }

            var reach = tuning.SwayLean * Mathf.Clamp01(strength);
            _lean.AddImpulse(away / length * (reach * tuning.KickPerReach));
        }

        /// <summary>The bounce paid the lance (it armed the ball): the cap swells and springs back.</summary>
        public void Pay(in MushroomLeanTuning tuning)
        {
            _pulse.AddImpulse(tuning.RewardPulse * tuning.KickPerReach);
        }

        /// <summary>
        ///     Moves the springs on by <paramref name="deltaTime" /> seconds. False once the cap has come to rest,
        ///     which is when it is set exactly to rest, so a caller may stop stepping it.
        /// </summary>
        public bool Step(float deltaTime, in MushroomLeanTuning tuning)
        {
            if (deltaTime > 0f)
            {
                var steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / MaxStepSeconds), 1, MaxStepsPerFrame);
                var step = Mathf.Min(deltaTime / steps, MaxStepSeconds);
                for (var i = 0; i < steps; i++)
                {
                    _lean.Step(Vector2.zero, tuning.Frequency, tuning.Damping, step);
                    _pulse.Step(0f, tuning.Frequency, tuning.Damping, step);
                    HoldWithinReach(tuning.MaxLean);
                }
            }

            if (!IsStill(tuning))
            {
                return true;
            }

            Reset();
            return false;
        }

        public void Reset()
        {
            _lean.Reset(Vector2.zero);
            _pulse.Reset(0f);
        }

        // Hits that pile up (two balls, or one back off a neighbour) may not carry the cap off its stem: past the
        // most it may lean it is held there, and keeps only the part of its motion that brings it back.
        private void HoldWithinReach(float maxLean)
        {
            var lean = _lean.Position;
            var distance = lean.magnitude;
            if (distance <= maxLean || distance < 1e-6f)
            {
                return;
            }

            var outward = lean / distance;
            _lean.Position = outward * maxLean;
            var leaving = Vector2.Dot(_lean.Velocity, outward);
            if (leaving > 0f)
            {
                _lean.Velocity -= outward * leaving;
            }
        }

        private bool IsStill(in MushroomLeanTuning tuning)
        {
            // A spring this near its rest and this slow never leaves the rest distance again.
            var restLean = tuning.RestLean;
            var restSpeed = restLean * tuning.AngularFrequency;
            var pulseSpeed = RestPulse * tuning.AngularFrequency;
            return _lean.Position.sqrMagnitude <= restLean * restLean
                && _lean.Velocity.sqrMagnitude <= restSpeed * restSpeed
                && Mathf.Abs(_pulse.Position) <= RestPulse
                && Mathf.Abs(_pulse.Velocity) <= pulseSpeed;
        }
    }
}
