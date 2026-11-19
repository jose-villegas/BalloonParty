using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Configuration.GridActors
{
    /// <summary>
    ///     Inspector-authored <see cref="IMushroomSettings" />. The field initialisers are the defaults the feature
    ///     was planned with, so a fresh instance plays the rules as settled.
    /// </summary>
    /// <remarks>
    ///     The layouts a level's walls are dealt from were baked with these numbers
    ///     (<see cref="MushroomWallCatalogue" />). Retuning the kick's cone or offsets, the radius, the chain's rules
    ///     or the placement means baking the catalogue again
    ///     (<c>Tools/BalloonParty/Bake Mushroom Wall Catalogue</c>): until it is, the layouts measure something
    ///     else than they were kept for, and <c>MushroomAimGridTests</c> fails on the numbers that no longer match.
    /// </remarks>
    [Serializable]
    internal class MushroomSettings : IMushroomSettings
    {
        // The slots the bushes are never left without, of the ones they share with the mushrooms.
        private const int BushSlotsKept = 2;

        // The furthest a cap's middle may be carried off its foot, as a share of its radius.
        private const float FurthestLeanShare = 0.75f;

        [Header("Chain")]
        [SerializeField] private MushroomChainRules _rules = new(chainTarget: 3);

        [Header("Bounce")]
        [Tooltip("On: a bounce leaving within the cone of a cap of another wall is bent toward it. Off: every " +
            "bounce is the mirror reflection.")]
        [SerializeField] private bool _forgivingKick = true;

        [SerializeField] private BumperKickTuning _kick =
            new(coneDegrees: 20f, minOffset: 0.15f, maxOffset: 0.9f);

        [Header("Size")]
        [Tooltip("A cap's own radius in world units, without the ball's. Slots are 0.75 apart.")]
        [SerializeField] [Min(0.01f)] private float _radius = 0.3f;

        [Header("Placement")]
        [SerializeField] private MushroomPlacementLimits _placement = new(
            wallRange: new Vector2Int(2, 4),
            capRange: new Vector2Int(4, 7),
            minSpacing: MushroomPlacementLimits.DefaultMinSpacing,
            keptClear: new Vector3Int(1, 1, 2),
            band: new MushroomLayoutBand(
                new Vector2(0.07f, 0.16f), minWindowDegrees: 2f, minArmingAngles: 20),
            cannon: new MushroomCannonClearance(minDistance: 3.25f, nearRange: 4f, maxShadowShare: 0.2f));

        [Tooltip("The most slots a level's permanent things take together. The mushrooms' caps are placed " +
            "first; the bushes get what is left.")]
        [SerializeField] [Min(0)] private int _permanentSlotLimit = 9;

        [Header("The cap's reaction")]
        [SerializeField] private MushroomLeanTuning _lean = new(
            leanPerSpeed: 0.014f,
            maxLean: 0.15f,
            frequency: 3.6f,
            damping: 0.16f,
            rewardPulse: 1f,
            restLean: 0.0015f,
            swayLean: 0.15f);

        [Header("Spores")]
        [SerializeField] private MushroomSporeTuning _spores = new(
            refractory: 0.4f,
            budget: 18f,
            budgetPerSecond: 3f,
            hit: new MushroomSporeHit(
                minSpeed: 2f, fullSpeed: 9.8f, length: 1f, endWidth: 0.6f, smoke: 0.9f, specks: 10, paidScale: 1.5f),
            cutScale: 2f,
            blastScale: 6f,
            blastDelay: 0.45f,
            blastFanDegrees: 170f,
            specks: new MushroomSporeSpecks(
                speed: 3.4f, seconds: 1.1f, size: 0.09f, green: new Color(0.3f, 1f, 0.5f, 1f),
                blue: new Color(0.25f, 0.7f, 1f, 1f)));

        [Header("A lance's cut")]
        [SerializeField] private MushroomCutTuning _cut = new(
            fallSeconds: 0.7f,
            partDistance: 0.16f,
            carryDistance: 0.08f,
            turnDegrees: 14f,
            endSize: 0.55f,
            regrowSeconds: 0.35f,
            regrowOvershoot: 0.1f,
            blown: new MushroomThrowTuning(seconds: 1.1f, distance: 0.9f, lift: 0.25f, turnDegrees: 40f));

        [Tooltip("How many of the board's beats a cap a lance cut stays gone. It grows back on the beat that " +
            "brings the count to this, so with the beat at 1.7 seconds 5 is between 6.8 and 8.5 seconds.")]
        [SerializeField] [Min(1)] private int _regrowBeats = 5;

        [Header("A blast")]
        [SerializeField] private MushroomBlastTuning _blast = new(swayShare: 2.4f);

        [Header("An idle cap")]
        [SerializeField] private MushroomIdleTuning _idle = new(
            breath: 0.08f,
            breathSeconds: 2.6f,
            phaseSpread: 0.1f,
            waveAcross: 0.2f,
            shadowLift: 0.8f,
            glowSwing: 0.4f,
            wartSpread: 0.08f,
            rockPerDisplacement: 0.16f,
            rockMost: 0.065f,
            rockSeconds: 0.45f,
            rockSwing: 0.6f);

        public MushroomChainRules Rules => _rules;
        public bool ForgivingKick => _forgivingKick;
        public BumperKickTuning Kick => _kick;
        public float Radius => _radius;
        public MushroomPlacementLimits Placement => _placement;
        public int PermanentSlotLimit => _permanentSlotLimit;
        public MushroomLeanTuning Lean => _lean;
        public MushroomIdleTuning Idle => _idle;
        public MushroomSporeTuning Spores => _spores;
        public MushroomCutTuning Cut => _cut;
        public int RegrowBeats => Mathf.Max(1, _regrowBeats);
        public MushroomBlastTuning Blast => _blast;

        /// <summary>
        ///     Adds one message to <paramref name="issues" /> per number that cannot work with the others; none
        ///     when the settings are sound. The rules still run on unsound settings (each guards itself), but not
        ///     as authored, so this is where the author is told.
        /// </summary>
        internal void Validate(string configName, List<string> issues)
        {
            var prefix = $"Mushroom settings ({configName}): ";
            if (_rules.ChainTarget > 0 && _rules.MaxBouncesPerStretch < _rules.ChainTarget)
            {
                issues.Add(prefix + $"the cap of {_rules.MaxBouncesPerStretch} bounces between walls ends a chain " +
                    $"before it is paid, which takes {_rules.ChainTarget}.");
            }

            if (_kick.MinOffset >= _kick.MaxOffset)
            {
                issues.Add(prefix + $"the kick's smallest offset ({_kick.MinOffset}) is not under its largest " +
                    $"({_kick.MaxOffset}), which leaves a chain one turn to make.");
            }

            var comeBackCone = 2f * Mathf.Asin(_kick.MinOffset) * Mathf.Rad2Deg;
            if (_forgivingKick && _kick.ConeDegrees <= comeBackCone)
            {
                issues.Add(prefix + $"the kick's cone ({_kick.ConeDegrees} degrees) is not wider than " +
                    $"{comeBackCone:F1}, twice the arcsine of its smallest offset, so two walls cannot send " +
                    "the ball back to each other.");
            }

            // The furthest the cap's middle is ever carried off its foot: a hit's lean with the field's rocking on
            // top of it. Past three quarters of the radius the cap stands beside its stem and not on it. (The
            // quad it is drawn on is sized from the same numbers, MushroomQuadRoom, so room is not the limit.)
            var furthest = _lean.MaxLean + _idle.RockMost;
            if (furthest > _radius * FurthestLeanShare)
            {
                issues.Add(prefix + $"the furthest lean ({_lean.MaxLean}) with the rocking's most ({_idle.RockMost}) " +
                    $"on top is over three quarters of the radius ({_radius}), which stands the cap beside its " +
                    "stem.");
            }

            ValidatePlacement(prefix, issues);
        }

        private void ValidatePlacement(string prefix, List<string> issues)
        {
            if (_radius <= 0f)
            {
                issues.Add(prefix + $"a cap's radius ({_radius}) must be above 0.");
            }

            if (_placement.MinCount > _placement.MaxCount)
            {
                issues.Add(prefix + $"the fewest walls ({_placement.MinCount}) is over the most " +
                    $"({_placement.MaxCount}).");
            }

            if (_placement.MinCaps > _placement.MaxCaps)
            {
                issues.Add(prefix + $"the fewest caps ({_placement.MinCaps}) is over the most " +
                    $"({_placement.MaxCaps}).");
            }

            if (_placement.MaxCaps < _placement.MaxCount)
            {
                issues.Add(prefix + $"the most caps ({_placement.MaxCaps}) is under the most walls " +
                    $"({_placement.MaxCount}), and a wall is at least one cap.");
            }

            if (_permanentSlotLimit - _placement.MaxCaps < BushSlotsKept)
            {
                issues.Add(prefix + $"the most caps ({_placement.MaxCaps}) leaves the bushes fewer than " +
                    $"{BushSlotsKept} of the {_permanentSlotLimit} slots the two share.");
            }

            // The ball's radius is the projectile's and not known here; without it two caps already need their
            // own two radii and the margin.
            var touching = _placement.MinSeparation(_radius);
            if (_placement.MinSpacing < touching)
            {
                issues.Add(prefix + $"the spacing ({_placement.MinSpacing}) is under {touching}, two cap radii " +
                    "and the margin. It is read as two contact radii and the margin instead, so no two walls " +
                    "close on a ball.");
            }

            var band = _placement.Band;
            if (band.MinArmingShare > band.MaxArmingShare)
            {
                issues.Add(prefix + $"the least share of aim angles that must arm ({band.MinArmingShare}) is over " +
                    $"the most that may ({band.MaxArmingShare}), so no layout can be accepted.");
            }

            var cannon = _placement.Cannon;
            if (cannon.NearRange > 0f && cannon.MaxShadowShare <= 0f)
            {
                issues.Add(prefix + "no aim angle may meet a cap within the cannon's near range " +
                    $"({cannon.NearRange}), which leaves the walls only the far end of the board.");
            }

            for (var walls = _placement.MinCount; walls <= _placement.MaxCount; walls++)
            {
                if (MushroomWallCatalogue.For(walls).Count == 0)
                {
                    issues.Add(prefix + $"the baked catalogue has no layout of {walls} walls, so a level that " +
                        "asks for them gets fewer or none. Bake it (Tools/BalloonParty/Bake Mushroom Wall Catalogue).");
                }
            }
        }
    }
}
