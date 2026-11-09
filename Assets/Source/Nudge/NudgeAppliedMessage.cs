using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Nudge
{
    /// <summary>One actor a nudge pushed: its slot, and where the push takes it at its peak (world units).</summary>
    public readonly struct NudgedActor
    {
        public readonly Vector2Int Slot;
        public readonly Vector2 Displacement;

        public NudgedActor(Vector2Int slot, Vector2 displacement)
        {
            Slot = slot;
            Displacement = displacement;
        }
    }

    /// <summary>
    ///     Published by <see cref="NudgeService" /> after it dispatches one nudge (a hit's neighbours, a deflect, or a
    ///     shockwave): every actor it pushed and how far. The list is the service's own buffer, valid only while the
    ///     message is handled.
    /// </summary>
    public readonly struct NudgeAppliedMessage
    {
        public readonly IReadOnlyList<NudgedActor> Actors;

        public NudgeAppliedMessage(IReadOnlyList<NudgedActor> actors)
        {
            Actors = actors;
        }
    }
}
