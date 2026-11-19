using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     Decides what a mushroom lets go when something happens to its cap (@ref plan_meadow_mushrooms): a
    ///     ball's hit puffs a small cone of spores the way the ball was going, scaled by its speed; a lance's cut
    ///     a little more, along the lance's way; a blast a wide burst, the way the cap is blown. Pure: it is told
    ///     the time and answers a <see cref="MushroomSporeBurst" />, and draws nothing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Kept quiet two ways.</b> A mushroom that has puffed for a hit says nothing for the tuned
    ///         refractory time, so a rally between two walls reads as a few puffs. And the whole board shares
    ///         one budget, counted in hits and coming back over time: what is asked when less is left comes out
    ///         that much smaller, down to nothing for a hit and to a floor for a blast, which is the one moment
    ///         that must always show. So a nuke's seven caps let go three bursts whole and four small ones.
    ///     </para>
    ///     <para>
    ///         It keeps one number a mushroom that has puffed and one for the budget; nothing is allocated for
    ///         a hit once a slot has been seen.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomSporePlanner
    {
        // Under this share of what was asked a hit or a cut is not worth letting go: a speck or two, a wisp.
        private const float LeastShare = 0.2f;

        // A blast always shows, however spent the board's budget: this share of it at the least.
        private const float BlastFloor = 0.35f;

        // A slow hit's cone is shorter, and its specks slower, but not by the whole of the speed's share.
        private const float SlowReach = 0.5f;

        // Nor is its smoke much thinner: the smoke field shows nothing of a stamp under about a third of its
        // opacity, so a slow hit's puff is smaller, not fainter.
        private const float SlowSmoke = 0.75f;

        // A cut's dust runs through the cap along the lance's way, wider than a hit's; a blast's cloud is wide
        // for its length, and thinner: the smoke field keeps a wide cloud for seconds where it lets a small puff
        // go in one, so a thick one would be a fog.
        private const float CutLength = 1.1f;
        private const float CutWidth = 1.2f;
        private const float CutFan = 1.5f;
        private const float BlastLength = 1.4f;
        private const float BlastWidth = 2f;
        private const float BlastSmoke = 0.6f;
        private const float BlastSpeed = 1.4f;

        private readonly Dictionary<Vector2Int, float> _quietUntil = new(16);

        private float _left;
        private float _asOf;
        private bool _started;

        /// <summary>
        ///     A ball going along <paramref name="incoming" /> at <paramref name="speed" /> bounced off the cap
        ///     at <paramref name="capCentre" />. False when it lets nothing go: too slow a touch, a mushroom
        ///     still quiet from its last puff, or a board that has just let too much go.
        /// </summary>
        internal bool TryHit(
            in MushroomSporeTuning tuning, float capRadius, Vector2Int slot, Vector2 capCentre, Vector2 incoming,
            float speed, bool paid, float now, out MushroomSporeBurst burst)
        {
            burst = default;
            var length = incoming.magnitude;
            if (length < 1e-5f || speed < tuning.MinSpeed)
            {
                return false;
            }

            if (_quietUntil.TryGetValue(slot, out var until) && now < until)
            {
                return false;
            }

            var strength = Mathf.InverseLerp(tuning.MinSpeed, tuning.FullSpeed, speed);
            var asked = Mathf.Max(strength, LeastShare) * (paid ? tuning.PaidScale : 1f);
            var share = Take(tuning, asked, now, 0f);
            if (share < LeastShare)
            {
                return false;
            }

            _quietUntil[slot] = now + tuning.Refractory;

            // Out of the cap's far rim, the way the ball was going.
            var way = incoming / length;
            var amount = asked * share;
            var reach = Mathf.Lerp(SlowReach, 1f, strength);
            burst = new MushroomSporeBurst(
                MushroomSporeKind.Hit, capCentre + (way * Mathf.Max(0f, capRadius)), way, tuning.Length * reach,
                tuning.EndWidth * reach, tuning.Smoke * Mathf.Lerp(SlowSmoke, 1f, strength) * share,
                Mathf.RoundToInt(tuning.Specks * amount),
                tuning.SpeckSpeed * reach, tuning.ConeDegrees, 0f);
            return true;
        }

        /// <summary>
        ///     A lance going along <paramref name="direction" /> cut the cap through <paramref name="point" />: a
        ///     little dust and a few specks along the lance's way as the cap opens. False when the board has
        ///     just let too much go.
        /// </summary>
        internal bool TryCut(
            in MushroomSporeTuning tuning, Vector2 point, Vector2 direction, float now, out MushroomSporeBurst burst)
        {
            burst = default;
            var length = direction.magnitude;
            if (length < 1e-5f || tuning.CutScale <= 0f)
            {
                return false;
            }

            var share = Take(tuning, tuning.CutScale, now, 0f);
            if (share < LeastShare)
            {
                return false;
            }

            burst = new MushroomSporeBurst(
                MushroomSporeKind.Cut, point, direction / length, tuning.Length * CutLength,
                tuning.EndWidth * CutWidth, tuning.Smoke * share,
                Mathf.RoundToInt(tuning.Specks * tuning.CutScale * share), tuning.SpeckSpeed,
                tuning.ConeDegrees * CutFan, 0f);
            return true;
        }

        /// <summary>
        ///     A blast blew the cap at <paramref name="capCentre" /> off along <paramref name="away" />: the
        ///     biggest release, wide, from where the cap sat, let go after the tuned delay. False only when the
        ///     blast's scale is tuned to nothing.
        /// </summary>
        internal bool TryBlast(
            in MushroomSporeTuning tuning, Vector2 capCentre, Vector2 away, float now, out MushroomSporeBurst burst)
        {
            burst = default;
            if (tuning.BlastScale <= 0f)
            {
                return false;
            }

            var length = away.magnitude;
            var way = length < 1e-5f ? Vector2.up : away / length;
            var share = Take(tuning, tuning.BlastScale, now, BlastFloor);
            burst = new MushroomSporeBurst(
                MushroomSporeKind.Blast, capCentre, way, tuning.Length * BlastLength, tuning.EndWidth * BlastWidth,
                tuning.Smoke * BlastSmoke * share,
                Mathf.RoundToInt(tuning.Specks * tuning.BlastScale * share), tuning.SpeckSpeed * BlastSpeed,
                tuning.BlastFanDegrees, tuning.BlastDelay);
            return true;
        }

        // Takes what is asked from the board's budget, or what is left of it, and answers the share that was
        // had: never under the floor, which is given even from an empty budget.
        private float Take(in MushroomSporeTuning tuning, float asked, float now, float floor)
        {
            if (!_started)
            {
                _started = true;
                _left = tuning.Budget;
                _asOf = now;
            }

            // A clock that ran back (a new run) gives nothing back and takes nothing away.
            var passed = Mathf.Max(0f, now - _asOf);
            _left = Mathf.Min(tuning.Budget, _left + (passed * tuning.BudgetPerSecond));
            _asOf = now;
            if (asked <= 0f)
            {
                return 0f;
            }

            var had = Mathf.Min(asked, _left);
            _left -= had;
            return Mathf.Max(had / asked, floor);
        }
    }
}
