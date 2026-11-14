using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>The pencil line's timing, read off the line prefab.</summary>
    internal readonly struct SnipeLanceStrokeTiming
    {
        internal readonly float TipSpeed;
        internal readonly float HoldSeconds;
        internal readonly float EraseSeconds;

        /// <param name="tipSpeed">World units a second the pencil tip runs; 0 or less draws the run at once.</param>
        internal SnipeLanceStrokeTiming(float tipSpeed, float holdSeconds, float eraseSeconds)
        {
            TipSpeed = Mathf.Max(0f, tipSpeed);
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            EraseSeconds = Mathf.Max(0f, eraseSeconds);
        }
    }

    /// <summary>
    ///     One pencil stroke's progress: the tip runs at a steady speed toward the end of the run, never past
    ///     it. The end can still be moving (the shot that is drawing it is still flying), so the stroke only
    ///     holds and then gets erased once the run is closed and the tip has reached its end.
    /// </summary>
    internal struct SnipeLanceStroke
    {
        private float _drawn;
        private float _sinceReached;
        private bool _reached;

        /// <summary>How far along the run the tip has drawn, in world units.</summary>
        internal float Drawn => _drawn;

        /// <param name="length">The run's length so far: up to the shot while it flies, then up to its wall.</param>
        /// <param name="closed">Whether the run has ended, so <paramref name="length" /> won't grow again.</param>
        /// <param name="erased">How far the erase has got, 0 (untouched) to 1 (gone).</param>
        /// <returns>False once the stroke is fully erased and its line can go back to the pool.</returns>
        internal bool Advance(float deltaTime, float length, bool closed, in SnipeLanceStrokeTiming timing,
            out float erased)
        {
            length = Mathf.Max(0f, length);
            _drawn = timing.TipSpeed > 0f ? Mathf.Min(_drawn + (timing.TipSpeed * deltaTime), length) : length;

            if (_reached)
            {
                _sinceReached += deltaTime;
            }
            else if (closed && _drawn >= length)
            {
                _reached = true;
            }

            erased = !_reached || _sinceReached < timing.HoldSeconds
                ? 0f
                : timing.EraseSeconds > 0f
                    ? Mathf.Clamp01((_sinceReached - timing.HoldSeconds) / timing.EraseSeconds)
                    : 1f;

            return erased < 1f;
        }
    }

    /// <summary>
    ///     The erase's order: the finished line is cut into chunks, and each is wiped away in its own slice of
    ///     the erase, the chunks taking their turn in a shuffled order, so the line is cleaned off piece by
    ///     piece rather than fading as one.
    /// </summary>
    internal static class SnipeLanceErase
    {
        /// <param name="erased">The whole erase's progress, 0 to 1.</param>
        /// <param name="turn">The chunk's place in the erase order, 0 first.</param>
        /// <param name="chunkCount">How many chunks the line was cut into.</param>
        /// <param name="chunkShare">The share of the erase each chunk takes to wipe; above 1/count, turns overlap.</param>
        /// <returns>How far that chunk is wiped, 0 (whole) to 1 (gone).</returns>
        internal static float ChunkErased(float erased, int turn, int chunkCount, float chunkShare)
        {
            if (chunkCount <= 1)
            {
                return Mathf.Clamp01(erased);
            }

            var share = Mathf.Clamp01(chunkShare);
            var start = turn / (float)(chunkCount - 1) * (1f - share);
            if (share <= 0f)
            {
                return erased >= start ? 1f : 0f;
            }

            return Mathf.Clamp01((erased - start) / share);
        }

        /// <summary>
        ///     The rub that takes a chunk away: across its wipe it smears out to one side, back across to the
        ///     other, and settles as it vanishes, like an eraser worked back and forth.
        /// </summary>
        /// <param name="wiped">How far the chunk is wiped, 0 to 1.</param>
        /// <returns>The sideways push, -1 to 1 of the smear distance; 0 when untouched and when gone.</returns>
        internal static float Smear(float wiped)
        {
            return Mathf.Sin(Mathf.Clamp01(wiped) * Mathf.PI * 2f);
        }

        /// <param name="wiped">How far the chunk is wiped, 0 to 1.</param>
        /// <returns>How much the chunk has spread, 0 untouched to 1 at the height of the rub.</returns>
        internal static float Spread(float wiped)
        {
            return Mathf.Sin(Mathf.Clamp01(wiped) * Mathf.PI);
        }
    }
}
