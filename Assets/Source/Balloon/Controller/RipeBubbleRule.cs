using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>When a bubble cluster is ripe, how long its fuse has left, how fast it turns, which balloons its
    /// ripple reaches and hits, where its bubbles go when the fuse runs out, and the timing of both endings'
    /// looks.</summary>
    internal static class RipeBubbleRule
    {
        /// <summary>The share of the burst delay over which a popped ripe cluster steps down to one bubble; the big
        /// bubble holds for the rest, then pops.</summary>
        internal const float FoldShare = 0.75f;

        /// <summary>How much faster the spiral arm, and the cluster with it, turns in the fuse's last seconds. Only a
        /// little: the thicker shedding shows the hurry, and an arm wound much tighter stops reading as a
        /// spiral.</summary>
        internal const float FastTurnSpeedup = 1.25f;

        /// <summary>A cluster is ripe at the merge cap; a cap of 0 or less ripens nothing.</summary>
        internal static bool IsRipe(int bubbles, int max)
        {
            return max > 0 && bubbles >= max;
        }

        /// <summary>
        ///     How many more clusters may be filled to the merge cap right now, which is what keeps ripe clusters few
        ///     and apart: none until <paramref name="cooldownBeats" /> of the bubbles' off-beats have passed since a
        ///     ripe cluster last ended (<paramref name="offBeatsSinceEnded" />, <see cref="int.MaxValue" /> when
        ///     none has), then what is left of <paramref name="maxRipe" /> (at least 1) after the
        ///     <paramref name="fullClusters" /> already at the cap.
        /// </summary>
        internal static int FillsLeft(int fullClusters, int maxRipe, int offBeatsSinceEnded, int cooldownBeats)
        {
            if (offBeatsSinceEnded < cooldownBeats)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.Max(1, maxRipe) - fullClusters);
        }

        /// <summary>
        ///     What a merge or a foam's hand-out may fill a cluster to right now: <paramref name="max" />, the merge
        ///     cap, while another cluster may still be filled (<see cref="FillsLeft" />), otherwise one bubble short of
        ///     it, where the cluster waits without ripening. It is a limit on what a cluster is given, never on what
        ///     it holds: one already at the cap keeps its bubbles. A cap of 1 or less has no bubble to hold back.
        /// </summary>
        internal static int FillLimit(int max, int fullClusters, int maxRipe, int offBeatsSinceEnded, int cooldownBeats)
        {
            if (max <= 1)
            {
                return max;
            }

            return FillsLeft(fullClusters, maxRipe, offBeatsSinceEnded, cooldownBeats) > 0 ? max : max - 1;
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
        ///     How fast a ripe cluster's spiral arm turns round it, in radians a second, never negative:
        ///     <paramref name="turnsPerSecond" /> while the fuse runs, <see cref="FastTurnSpeedup" /> times that once
        ///     its throbs are the <paramref name="fast" /> ones. The cluster's own spin is the same number
        ///     (<see cref="ClusterSpin" />), so the two turn as one.
        /// </summary>
        internal static float SpiralTurnRate(float turnsPerSecond, bool fast)
        {
            return Mathf.Max(0f, turnsPerSecond) * Mathf.PI * 2f * (fast ? FastTurnSpeedup : 1f);
        }

        /// <summary>
        ///     The soap shader's <c>_RotationSpeed</c> that turns a ripe cluster with its spiral arm, whose source
        ///     turns <paramref name="turnDirection" /> (+1 counter-clockwise on screen, -1 clockwise) at
        ///     <see cref="SpiralTurnRate" />. The sign flips because the shader turns the point it samples, not the
        ///     picture: the drawn bubbles go round the other way from the angle it is given.
        /// </summary>
        internal static float ClusterSpin(float turnDirection, float turnsPerSecond, bool fast)
        {
            return -Mathf.Sign(turnDirection) * SpiralTurnRate(turnsPerSecond, fast);
        }

        /// <summary>
        ///     Whether a ripe cluster's burst at <paramref name="burstCentre" /> reaches the slot whose centre is
        ///     <paramref name="slotCentre" />: within <paramref name="radius" /> world units, the edge included. It is
        ///     measured between slot centres, never where a balloon is drawn on its way to one, so the game and the
        ///     shot solver agree on a board in motion. A radius of 0 or less reaches nothing.
        /// </summary>
        internal static bool RippleReaches(Vector2 burstCentre, Vector2 slotCentre, float radius)
        {
            return radius > 0f && (slotCentre - burstCentre).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        ///     Whether a ripple hits this actor once it reaches it (<see cref="RippleReaches" />): a balloon, as an
        ///     item's hit would, other than a reward balloon or one that bounces an ordinary hit (the unbreakable, a
        ///     tough with more than one hit left), whose <c>Deflect</c> would act on the shot that set the ripple off.
        ///     Other clusters are hit, ripe ones too.
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
        ///     as leftovers. Only <paramref name="clusterFills" /> clusters (<see cref="FillsLeft" />) take the last
        ///     bubble of their room, the one that fills them to the cap: counted as the plan fills them, so one
        ///     hand-out never fills more than that, and every other cluster stops a bubble short. Both arrays are
        ///     indexed by direction and used up as the plan takes from them.
        /// </summary>
        internal static int PlanHandout(int bubbles, int[] coatRoom, int[] clusterRoom, int clusterFills)
        {
            var handout = 0;
            var left = Mathf.Clamp(bubbles, 0, RipeBubbleBurstMessage.MaxHandout);
            // Nothing limits how many balloons are coated up to their last layer.
            var coatFills = int.MaxValue;
            var gave = true;
            while (left > 0 && gave)
            {
                gave = GiveRound(ref handout, ref left, coatRoom, FoamGift.Coat, ref coatFills);
                gave |= GiveRound(ref handout, ref left, clusterRoom, FoamGift.Cluster, ref clusterFills);
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

        /// <summary>Seconds between the steps of a fold from <paramref name="from" /> bubbles: they share
        /// <see cref="FoldShare" /> of <paramref name="burstSeconds" /> evenly. 0 when there is nothing to step
        /// through.</summary>
        internal static float FoldStepSeconds(float burstSeconds, int from)
        {
            return from > 1 && burstSeconds > 0f ? FoldShare * burstSeconds / (from - 1) : 0f;
        }

        /// <summary>
        ///     The bubbles a popped ripe cluster shows <paramref name="elapsed" /> seconds into its fold: from
        ///     <paramref name="from" /> down one at a time at even steps (<see cref="FoldStepSeconds" />), then 1
        ///     until it pops.
        /// </summary>
        internal static int FoldCount(float elapsed, float burstSeconds, int from)
        {
            var step = FoldStepSeconds(burstSeconds, from);
            if (step <= 0f)
            {
                return 1;
            }

            return Mathf.Clamp(from - FoldStepsTaken(elapsed, step, from), 1, from);
        }

        /// <summary>
        ///     The folding cluster's settled scale over its own while it shows <paramref name="shown" /> of the
        ///     <paramref name="from" /> bubbles it started with. The shader packs fewer bubbles closer in, so the scale
        ///     makes up for it and the cluster keeps its outline, and each step adds an even share of
        ///     <paramref name="growth" /> on top: at one bubble it is a single bubble <see cref="FoldedSize" /> times
        ///     the size of the whole cluster it was. <paramref name="bubbleRadius" /> is the material's
        ///     (<see cref="SoapBubbleShader.BubbleRadiusOf" />).
        /// </summary>
        internal static float FoldScale(int shown, int from, float bubbleRadius, float growth)
        {
            if (from <= 1)
            {
                return 1f;
            }

            var share = Mathf.Clamp01((float)(from - shown) / (from - 1));
            var outline = SoapBubbleShader.LayoutReach(from, bubbleRadius)
                          / SoapBubbleShader.LayoutReach(shown, bubbleRadius);
            return outline * (1f + (Mathf.Max(0f, growth) * share));
        }

        /// <summary>The one bubble a fold ends on, over the size of the whole cluster it started as.</summary>
        internal static float FoldedSize(float growth)
        {
            return 1f + Mathf.Max(0f, growth);
        }

        /// <summary>
        ///     The pump on top of <see cref="FoldScale" /> <paramref name="elapsed" /> seconds into a fold: each count
        ///     shown, the full cluster at the hit included, kicks the scale out by <paramref name="strength" /> the
        ///     moment it shows and eases back to 1 by the next step, so a fold from six reads as six pumps. The last
        ///     one settles on the one big bubble, which holds at 1 until the pop.
        /// </summary>
        internal static float FoldPulse(float elapsed, float burstSeconds, int from, float strength)
        {
            var step = FoldStepSeconds(burstSeconds, from);
            if (step <= 0f || strength <= 0f)
            {
                return 1f;
            }

            var sinceStep = Mathf.Max(0f, elapsed) - (FoldStepsTaken(elapsed, step, from) * step);
            var left = 1f - Mathf.Clamp01(sinceStep / step);
            return 1f + (strength * left * left);
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

        // The steps of a fold behind it at elapsed, never more than the fold has.
        private static int FoldStepsTaken(float elapsed, float step, int from)
        {
            return Mathf.Clamp(Mathf.FloorToInt((elapsed / step) + 1e-4f), 0, from - 1);
        }

        // One bubble to each taker with room, in direction order. True if any was given. A taker's last bubble of
        // room uses up one of fills; with none left, the taker stays a bubble short.
        private static bool GiveRound(ref int handout, ref int left, int[] room, FoamGift gift, ref int fills)
        {
            var gave = false;
            for (var direction = 0; direction < room.Length && left > 0; direction++)
            {
                if (room[direction] <= 0)
                {
                    continue;
                }

                if (room[direction] == 1)
                {
                    if (fills <= 0)
                    {
                        continue;
                    }

                    fills--;
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
