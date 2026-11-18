using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>When a bubble cluster is ripe, how long its fuse has left, which neighbours its ripple hits, where its
    /// bubbles go when the fuse runs out, and the timing of both endings' looks.</summary>
    internal static class RipeBubbleRule
    {
        /// <summary>The share of the burst delay over which a popped ripe cluster steps down to one bubble; the big
        /// bubble holds for the rest, then pops.</summary>
        internal const float FoldShare = 0.75f;

        /// <summary>How much a folding cluster grows past its own size by the time it is one bubble.</summary>
        internal const float FoldGrowth = 0.1f;

        /// <summary>The one bubble a fold ends on, over the size of the whole cluster it started as.</summary>
        internal const float FoldedSize = 1f + FoldGrowth;

        /// <summary>A cluster is ripe at the merge cap; a cap of 0 or less ripens nothing.</summary>
        internal static bool IsRipe(int bubbles, int max)
        {
            return max > 0 && bubbles >= max;
        }

        /// <summary>Seconds of level clock left before <paramref name="deadline" />, never below 0.</summary>
        internal static float FuseLeft(float deadline, float now)
        {
            return deadline > now ? deadline - now : 0f;
        }

        /// <summary>
        ///     Whether a ripe cluster's fuse throbs as it runs from <paramref name="before" /> seconds left to
        ///     <paramref name="after" />: once as it crosses each whole second, and each half second in the last
        ///     <paramref name="fastSeconds" />. A step that crosses two beats throbs once; a fuse that holds (the level
        ///     clock stopped), runs back up (ripening) or has run out (the burst takes over) does not throb.
        /// </summary>
        internal static bool Throbs(float before, float after, float fastSeconds)
        {
            return after > 0f && after < before && ThrobBeat(after, fastSeconds) < ThrobBeat(before, fastSeconds);
        }

        /// <summary>Whether a throb at <paramref name="left" /> seconds is one of the fast ones at the end.</summary>
        internal static bool IsFastThrob(float left, float fastSeconds)
        {
            return left <= fastSeconds;
        }

        /// <summary>
        ///     How far toward its warning tint a ripe cluster is: <paramref name="start" /> the moment it ripens,
        ///     so the player sees at once that it has turned, then evenly up to 1 as the fuse runs out. 0 for a
        ///     fuse of no length.
        /// </summary>
        internal static float WarnAmount(float fuseLeft, float fuseSeconds, float start)
        {
            if (fuseSeconds <= 0f)
            {
                return 0f;
            }

            var run = Mathf.Clamp01(1f - (fuseLeft / fuseSeconds));
            return Mathf.Lerp(Mathf.Clamp01(start), 1f, run);
        }

        /// <summary>
        ///     Whether a ripple hits this neighbour: a balloon, as an item's hit would, other than a reward balloon or
        ///     one that bounces an ordinary hit (the unbreakable, a tough with more than one hit left), whose
        ///     <c>Deflect</c> would act on the shot that set the ripple off. Other clusters are hit, ripe ones too.
        /// </summary>
        internal static bool RippleTakes(ISlotActor actor)
        {
            return actor is IBalloonModel balloon
                   && !balloon.TypeName.IsReward()
                   && actor is not IDeflectsShots { DeflectsOrdinaryHit: true };
        }

        /// <summary>
        ///     Whether a cluster whose fuse ran out can coat this neighbour: a plain balloon (<see cref="ICoatable" />,
        ///     item carriers and rainbows included) wearing fewer than <paramref name="maxLayers" />, never a reward
        ///     balloon. Toughs, unbreakables and grid actors take none: only the plain balloon wears a coat. A
        ///     neighbouring cluster takes a bubble instead (<see cref="ClusterRoom" />).
        /// </summary>
        internal static bool FoamTakes(ISlotActor actor, int maxLayers)
        {
            return actor is ICoatable coatable
                   && coatable.Coat.Value < maxLayers
                   && actor is IBalloonModel balloon
                   && !balloon.TypeName.IsReward();
        }

        /// <summary>How many bubbles a neighbouring cluster can take from a hand-out before it is full at
        /// <paramref name="max" />, as a merge would fill it; 0 for anything that is not a cluster.</summary>
        internal static int ClusterRoom(ISlotActor actor, int max)
        {
            return actor is BubbleClusterModel cluster ? Mathf.Max(0, max - cluster.HitsRemaining.Value) : 0;
        }

        /// <summary>
        ///     Where a cluster whose fuse ran out hands its <paramref name="bubbles" /> (at most
        ///     <see cref="RipeBubbleBurstMessage.MaxHandout" />), encoded as <see cref="RipeBubbleBurstMessage.Handout" />.
        ///     One bubble per taker a round, in direction order, coats (<paramref name="coatRoom" />, layers each plain
        ///     neighbour can still take) before clusters (<paramref name="clusterRoom" />, bubbles each neighbouring
        ///     cluster can still take); rounds run until the bubbles or the room run out, and any bubbles left over pop
        ///     as leftovers. Both arrays are indexed by direction and used up as the plan takes from them.
        /// </summary>
        internal static int PlanHandout(int bubbles, int[] coatRoom, int[] clusterRoom)
        {
            var handout = 0;
            var left = Mathf.Clamp(bubbles, 0, RipeBubbleBurstMessage.MaxHandout);
            var gave = true;
            while (left > 0 && gave)
            {
                gave = GiveRound(ref handout, ref left, coatRoom, FoamGift.Coat);
                gave |= GiveRound(ref handout, ref left, clusterRoom, FoamGift.Cluster);
            }

            for (; left > 0; left--)
            {
                handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Leftover, -1);
            }

            return handout;
        }

        /// <summary>The neighbours a hand-out coats, a bit per direction as
        /// <see cref="RipeBubbleBurstMessage.Neighbours" /> carries them.</summary>
        internal static int CoatedNeighbours(int handout)
        {
            var coated = 0;
            var count = RipeBubbleBurstMessage.HandoutCount(handout);
            for (var bubble = 0; bubble < count; bubble++)
            {
                if (RipeBubbleBurstMessage.HandoutGift(handout, bubble, out var direction) == FoamGift.Coat)
                {
                    coated |= 1 << direction;
                }
            }

            return coated;
        }

        /// <summary>
        ///     The bubbles a popped ripe cluster shows <paramref name="elapsed" /> seconds into its fold: from
        ///     <paramref name="from" /> down one at a time at even steps over <see cref="FoldShare" /> of
        ///     <paramref name="burstSeconds" />, then 1 until it pops.
        /// </summary>
        internal static int FoldCount(float elapsed, float burstSeconds, int from)
        {
            if (from <= 1 || burstSeconds <= 0f)
            {
                return 1;
            }

            var step = FoldShare * burstSeconds / (from - 1);
            var stepsTaken = Mathf.FloorToInt((elapsed / step) + 1e-4f);
            return Mathf.Clamp(from - stepsTaken, 1, from);
        }

        /// <summary>
        ///     The folding cluster's scale over its own while it shows <paramref name="shown" /> of the
        ///     <paramref name="from" /> bubbles it started with. The shader packs fewer bubbles closer in, so the scale
        ///     makes up for it and the cluster keeps its outline, growing a little more each step: at one bubble it
        ///     is a single bubble <see cref="FoldedSize" /> times the size of the whole cluster it was.
        ///     <paramref name="bubbleRadius" /> is the material's (<see cref="SoapBubbleShader.BubbleRadiusOf" />).
        /// </summary>
        internal static float FoldScale(int shown, int from, float bubbleRadius)
        {
            if (from <= 1)
            {
                return 1f;
            }

            var share = Mathf.Clamp01((float)(from - shown) / (from - 1));
            var outline = SoapBubbleShader.LayoutReach(from, bubbleRadius)
                          / SoapBubbleShader.LayoutReach(shown, bubbleRadius);
            return outline * (1f + (FoldGrowth * share));
        }

        /// <summary>Seconds from the fuse running out to the hand-out's bubble <paramref name="bubble" /> (0 first)
        /// leaving: one every <paramref name="step" />, the first a step in.</summary>
        internal static float FoamDepartSeconds(int bubble, float step)
        {
            return (bubble + 1) * Mathf.Max(0f, step);
        }

        /// <summary>How many of its <paramref name="bubbles" /> a cluster handing them out still shows
        /// <paramref name="elapsed" /> seconds after its fuse ran out; 0 once the last has left.</summary>
        internal static int FoamShownCount(int bubbles, float elapsed, float step)
        {
            if (step <= 0f)
            {
                return 0;
            }

            var left = bubbles - Mathf.FloorToInt((elapsed / step) + 1e-4f);
            return Mathf.Clamp(left, 0, bubbles);
        }

        /// <summary>Seconds a cluster handing out <paramref name="bubbles" /> stays on screen: until the last
        /// leaves.</summary>
        internal static float FoamLeaveSeconds(int bubbles, float step)
        {
            return bubbles > 0 ? FoamDepartSeconds(bubbles - 1, step) : 0f;
        }

        // One bubble to each taker with room, in direction order. True if any was given.
        private static bool GiveRound(ref int handout, ref int left, int[] room, FoamGift gift)
        {
            var gave = false;
            for (var direction = 0; direction < room.Length && left > 0; direction++)
            {
                if (room[direction] <= 0)
                {
                    continue;
                }

                room[direction]--;
                left--;
                handout = RipeBubbleBurstMessage.AddGift(handout, gift, direction);
                gave = true;
            }

            return gave;
        }

        // Twice the next beat at or above left, in half seconds: whole seconds above the fast window, half seconds
        // inside it. A throb is a drop in this number, so it is a whole count and needs no float comparison of beats.
        private static int ThrobBeat(float left, float fastSeconds)
        {
            return left > fastSeconds ? 2 * Mathf.CeilToInt(left) : Mathf.CeilToInt(left * 2f);
        }
    }
}
