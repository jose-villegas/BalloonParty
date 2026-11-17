using UnityEngine;

namespace BalloonParty.Shared.Math
{
    /// <summary>How a <see cref="SpringChain" /> moves: its springs, gravity, damping and fixed step.</summary>
    internal readonly struct SpringChainTuning
    {
        internal readonly float Stiffness;
        internal readonly float Gravity;
        internal readonly float Damping;
        internal readonly float StepSeconds;
        internal readonly int Iterations;

        internal SpringChainTuning(float stiffness, float gravity, float damping, float stepSeconds, int iterations)
        {
            Stiffness = stiffness;
            Gravity = gravity;
            Damping = damping;
            StepSeconds = stepSeconds;
            Iterations = iterations;
        }
    }

    /// <summary>
    ///     A line of points joined by springs between two pinned ends, simulated the way the launch screen's spring
    ///     line is (<c>UI/ClothRibbon/RibbonCloth</c>): position-based, at fixed steps, each step letting the points
    ///     carry on and fall, then a few passes pulling every spring back toward its rest length. Its springs give,
    ///     so it sags when its ends close in, stretches when they part, and swings as they move.
    /// </summary>
    internal sealed class SpringChain
    {
        private const int MaxStepsPerFrame = 8;

        // A chain just starting to grow is this share of its length, so its springs never fold to nothing.
        private const float MinGrowth = 0.05f;

        // Share of the way to its target a loose end moves each step at full reach.
        private const float ReachPull = 0.2f;

        private readonly Vector3[] _position;
        private readonly Vector3[] _previous;

        private Vector3 _from;
        private Vector3 _to;
        private float _carry;

        internal int Points => _position.Length;

        internal SpringChain(int segments)
        {
            _position = new Vector3[Mathf.Max(2, segments) + 1];
            _previous = new Vector3[_position.Length];
        }

        internal Vector3 Point(int index)
        {
            return _position[index];
        }

        /// <summary>Lays the chain straight from <paramref name="from" /> to <paramref name="to" />, at rest.</summary>
        internal void Place(Vector3 from, Vector3 to)
        {
            for (var i = 0; i < _position.Length; i++)
            {
                _position[i] = Vector3.Lerp(from, to, i / (float)(_position.Length - 1));
                _previous[i] = _position[i];
            }

            _from = from;
            _to = to;
            _carry = 0f;
        }

        /// <summary>
        ///     Moves the ends to <paramref name="from" /> and <paramref name="to" /> over the frame's fixed steps (the
        ///     ends glide there step by step, so a quick move swings the chain rather than snapping it) and lets the
        ///     rest follow, every spring held toward <paramref name="restLength" /> over the whole chain.
        ///     <paramref name="reach" /> below 1 leaves the far end loose: the chain is that share of its length, grown
        ///     out of <paramref name="from" />, and its loose end reaches toward <paramref name="to" />, harder the
        ///     further it has grown. At 1 the far end is tied on at <paramref name="to" />.
        /// </summary>
        internal void Step(Vector3 from, Vector3 to, float restLength, float deltaTime, in SpringChainTuning tuning,
            float reach = 1f)
        {
            var tied = reach >= 1f;
            _carry += deltaTime;
            var steps = Mathf.FloorToInt(_carry / tuning.StepSeconds);
            if (steps <= 0)
            {
                return;
            }

            _carry = steps > MaxStepsPerFrame ? 0f : _carry - steps * tuning.StepSeconds;
            steps = Mathf.Min(steps, MaxStepsPerFrame);
            var startFrom = _from;
            var startTo = _to;
            var last = _position.Length - 1;
            var segmentRest = restLength * (tied ? 1f : Mathf.Max(MinGrowth, reach)) / last;
            var fall = Vector3.down * (tuning.Gravity * tuning.StepSeconds * tuning.StepSeconds);
            var pull = tied ? 0f : Mathf.Clamp01(reach) * Mathf.Clamp01(reach) * ReachPull;
            var free = tied ? last : last + 1;
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                for (var i = 1; i < free; i++)
                {
                    var carried = (_position[i] - _previous[i]) * (1f - tuning.Damping);
                    _previous[i] = _position[i];
                    _position[i] += carried + fall;
                }

                _position[0] = _previous[0] = Vector3.Lerp(startFrom, from, t);
                if (tied)
                {
                    _position[last] = _previous[last] = Vector3.Lerp(startTo, to, t);
                }
                else
                {
                    _position[last] += (to - _position[last]) * pull;
                }

                for (var pass = 0; pass < tuning.Iterations; pass++)
                {
                    SolveSprings(segmentRest, tuning.Stiffness, tied);
                }
            }

            _from = from;
            // A loose end ties on from where it has reached, gliding the rest of the way.
            _to = tied ? to : _position[last];
        }

        // The near end is pinned, and the far end while tied: a spring with one pinned end moves only its other
        // point, the whole correction.
        private void SolveSprings(float rest, float stiffness, bool tied)
        {
            var last = _position.Length - 1;
            for (var i = 0; i < last; i++)
            {
                var gap = _position[i + 1] - _position[i];
                var length = gap.magnitude;
                if (length < 1e-6f)
                {
                    continue;
                }

                var giveA = i == 0 ? 0f : 1f;
                var giveB = i + 1 == last && tied ? 0f : 1f;
                var give = giveA + giveB;
                if (give <= 0f)
                {
                    continue;
                }

                var correction = gap * ((length - rest) / length * stiffness / give);
                _position[i] += correction * giveA;
                _position[i + 1] -= correction * giveB;
            }
        }
    }
}
