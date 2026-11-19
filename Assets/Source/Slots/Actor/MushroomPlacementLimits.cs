using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     What a layout of mushroom walls must measure to be dealt: flown from every angle the thrower can aim
    ///     at, on a board with nothing else on it, how many of those angles arm the lance and how wide the widest
    ///     run of them is. A layout is accepted by this and not by its shape.
    /// </summary>
    [Serializable]
    internal struct MushroomLayoutBand
    {
        [Tooltip("The least share of the aim angles that must arm the lance, 0 to 1. Under it the walls are " +
            "decoration.")]
        [SerializeField] [Range(0f, 1f)] private float _minArmingShare;

        [Tooltip("The most of the aim angles that may arm the lance, 0 to 1. Over it the lance is free.")]
        [SerializeField] [Range(0f, 1f)] private float _maxArmingShare;

        [Tooltip("The least the widest unbroken run of arming angles spans, in degrees: a window a player can " +
            "aim into, where scattered single angles are a lottery.")]
        [SerializeField] [Min(0f)] private float _minWindowDegrees;

        [Tooltip("The fewest aim angles that must arm the lance, whatever share that is of the grid.")]
        [SerializeField] [Min(0)] private int _minArmingAngles;

        public float MinArmingShare => _minArmingShare;
        public float MaxArmingShare => _maxArmingShare;
        public float MinWindowDegrees => _minWindowDegrees;
        public int MinArmingAngles => _minArmingAngles;

        /// <param name="armingShare">The least (x) and the most (y) share of the aim angles that arm, 0 to 1.</param>
        internal MushroomLayoutBand(Vector2 armingShare, float minWindowDegrees, int minArmingAngles)
        {
            _minArmingShare = armingShare.x;
            _maxArmingShare = armingShare.y;
            _minWindowDegrees = minWindowDegrees;
            _minArmingAngles = minArmingAngles;
        }

        /// <summary>
        ///     Whether a layout that arms on <paramref name="armingAngles" /> of <paramref name="angles" /> aim
        ///     angles, the widest run of them <paramref name="widestRun" /> angles long, is one to deal.
        ///     <paramref name="stepDegrees" /> is the thrower's aim step, which a run of angles spans one of each.
        /// </summary>
        internal bool Accepts(int armingAngles, int widestRun, int angles, float stepDegrees)
        {
            if (angles <= 0)
            {
                return false;
            }

            var share = armingAngles / (float)angles;
            return armingAngles >= _minArmingAngles && share >= _minArmingShare && share <= _maxArmingShare
                && widestRun * stepDegrees >= _minWindowDegrees;
        }
    }

    /// <summary>
    ///     The room a layout leaves the cannon: no cap close to it, and no wall broad in front of it. Measured from
    ///     the cannon at rest on a board with nothing else on it, when a layout is baked.
    /// </summary>
    [Serializable]
    internal struct MushroomCannonClearance
    {
        [Tooltip("The least a cap's centre stands from the point the cannon turns about at rest, in world units.")]
        [SerializeField] [Min(0f)] private float _minDistance;

        [Tooltip("How far from the muzzle, along a shot's first straight leg, a cap still counts as in front of " +
            "the cannon, in world units.")]
        [SerializeField] [Min(0f)] private float _nearRange;

        [Tooltip("The most of the aim angles whose first straight leg may meet a cap within the near range, 0 to " +
            "1. It is what keeps a wall from standing side-on across the cannon's view, while a cap low by a side " +
            "wall, or a wall seen end-on, takes few angles and may stand.")]
        [SerializeField] [Range(0f, 1f)] private float _maxShadowShare;

        public float MinDistance => _minDistance;
        public float NearRange => _nearRange;
        public float MaxShadowShare => _maxShadowShare;

        internal MushroomCannonClearance(float minDistance, float nearRange, float maxShadowShare)
        {
            _minDistance = minDistance;
            _nearRange = nearRange;
            _maxShadowShare = maxShadowShare;
        }

        /// <summary>
        ///     Whether a layout whose nearest cap stands <paramref name="nearestCap" /> from the cannon, and which
        ///     a shot's first leg meets within the near range on <paramref name="shadowedAngles" /> of
        ///     <paramref name="angles" /> aim angles, leaves the cannon its room.
        /// </summary>
        internal bool Allows(float nearestCap, int shadowedAngles, int angles)
        {
            return nearestCap >= _minDistance && shadowedAngles <= _maxShadowShare * Mathf.Max(0, angles);
        }
    }

    /// <summary>
    ///     What a level's mushrooms keep to: how many walls of caps, how long a wall is, how many caps in all, how
    ///     far apart the walls stand, which slots are off limits, and what a layout must measure to be dealt.
    ///     Authored on the mushroom settings (<c>IMushroomSettings.Placement</c>). The layouts themselves are
    ///     baked to these limits (<see cref="MushroomWallCatalogue" />); a level picks one.
    /// </summary>
    [Serializable]
    internal struct MushroomPlacementLimits
    {
        /// <summary>
        ///     The spacing a layout keeps when none is authored (see <see cref="MinSpacing" />): two and a half
        ///     slots. Slots stand on a lattice, so two of them are 0.75, 1.3, 1.5, 1.98, 2.25 or 2.6 apart and so
        ///     on: 1.9 keeps the nearest caps of two walls 1.98 or more apart.
        /// </summary>
        internal const float DefaultMinSpacing = 1.9f;

        /// <summary>The caps of one wall stand in neighbouring slots, in one line: a wall is never longer than this.</summary>
        internal const int LongestWall = 3;

        // The room two walls keep beyond touching, in world units, when a ball passes between them.
        private const float RoomMargin = 0.05f;

        [Tooltip("Fewest walls worth placing. When no layout of the count asked for fits the board, one wall " +
            "fewer is tried down to this, then none.")]
        [SerializeField] [Min(1)] private int _minCount;

        [Tooltip("Most walls a level has: the count on the last level of a meadow stretch.")]
        [SerializeField] [Min(1)] private int _maxCount;

        [Tooltip("Fewest caps a level's walls hold together.")]
        [SerializeField] [Min(1)] private int _minCaps;

        [Tooltip("Most caps a level's walls hold together. The bushes get what this leaves of the slots the two " +
            "share.")]
        [SerializeField] [Min(1)] private int _maxCaps;

        [Tooltip("Least distance between two walls, nearest cap to nearest cap, centre to centre, in world " +
            "units: they stand apart like a pinball table's bumpers. Slots are 0.75 apart along a row. Never " +
            "read as less than the room a ball needs to pass between two caps; 0 or less reads as the shipped " +
            "spacing.")]
        [SerializeField] [Min(0f)] private float _minSpacing;

        [Tooltip("Columns at each side of the grid no cap is placed in.")]
        [SerializeField] [Min(0)] private int _edgeColumnsKeptClear;

        [Tooltip("Rows at the top of the grid no cap is placed in.")]
        [SerializeField] [Min(0)] private int _topRowsKeptClear;

        [Tooltip("Rows at the bottom of the grid no cap is placed in: the rows the balloon spawner needs.")]
        [SerializeField] [Min(0)] private int _bottomRowsKeptClear;

        [Tooltip("What a layout must measure, flown from every aim angle on an empty board, to be in the " +
            "catalogue the levels are dealt from.")]
        [SerializeField] private MushroomLayoutBand _band;

        [Tooltip("The room a layout must leave the cannon to be in the catalogue.")]
        [SerializeField] private MushroomCannonClearance _cannon;

        /// <summary>Fewest walls worth placing.</summary>
        public int MinCount => _minCount;

        /// <summary>Most walls a level has.</summary>
        public int MaxCount => _maxCount;

        public int MinCaps => _minCaps;
        public int MaxCaps => _maxCaps;

        /// <summary>
        ///     The least two walls of a layout stand apart, nearest cap to nearest cap, as authored. Never 0: a
        ///     limit authored at nothing keeps <see cref="DefaultMinSpacing" />, so the rule cannot be lost by
        ///     leaving a field empty. What a layout is held to is <see cref="EffectiveMinSpacing" />.
        /// </summary>
        public float MinSpacing => _minSpacing > 0f ? _minSpacing : DefaultMinSpacing;

        public int EdgeColumnsKeptClear => _edgeColumnsKeptClear;
        public int TopRowsKeptClear => _topRowsKeptClear;
        public int BottomRowsKeptClear => _bottomRowsKeptClear;
        public MushroomLayoutBand Band => _band;
        public MushroomCannonClearance Cannon => _cannon;

        /// <param name="wallRange">Fewest (x) and most (y) walls.</param>
        /// <param name="capRange">Fewest (x) and most (y) caps in all.</param>
        /// <param name="keptClear">Columns at each side (x), rows at the top (y) and rows at the bottom (z) no
        /// cap is placed in.</param>
        internal MushroomPlacementLimits(
            Vector2Int wallRange, Vector2Int capRange, float minSpacing, Vector3Int keptClear, MushroomLayoutBand band,
            MushroomCannonClearance cannon)
        {
            _cannon = cannon;
            _minCount = wallRange.x;
            _maxCount = wallRange.y;
            _minCaps = capRange.x;
            _maxCaps = capRange.y;
            _minSpacing = minSpacing;
            _edgeColumnsKeptClear = keptClear.x;
            _topRowsKeptClear = keptClear.y;
            _bottomRowsKeptClear = keptClear.z;
            _band = band;
        }

        /// <summary>
        ///     The most caps a level of <paramref name="walls" /> walls may hold: every wall at its longest, but
        ///     never over <see cref="MaxCaps" />. It is what the level generator takes from the slots the
        ///     mushrooms share with the bushes, since which layout a level is dealt is not known until it is.
        /// </summary>
        internal int MaxCapsFor(int walls)
        {
            return Mathf.Max(0, Mathf.Min(_maxCaps, walls * LongestWall));
        }

        /// <summary>
        ///     The least two caps of different walls may stand apart, centre to centre, for a ball to pass between
        ///     them with room to spare: two contact radii and a margin. <paramref name="contactRadius" /> is a
        ///     cap's radius plus the ball's.
        /// </summary>
        internal float MinSeparation(float contactRadius)
        {
            return (2f * contactRadius) + RoomMargin;
        }

        /// <summary>
        ///     The least two walls of a layout really stand apart: <see cref="MinSpacing" />, but never under
        ///     <see cref="MinSeparation" />, whatever is authored.
        /// </summary>
        internal float EffectiveMinSpacing(float contactRadius)
        {
            return Mathf.Max(MinSpacing, MinSeparation(contactRadius));
        }

        /// <summary>These limits with another number of bottom rows kept clear, and nothing else changed.</summary>
        internal MushroomPlacementLimits WithBottomRows(int bottomRowsKeptClear)
        {
            var limits = this;
            limits._bottomRowsKeptClear = bottomRowsKeptClear;
            return limits;
        }

        /// <summary>Whether a cap may stand in <paramref name="slot" /> of a grid of <paramref name="gridSize" />:
        /// not in a column or a row these limits keep clear.</summary>
        internal bool Allows(Vector2Int slot, Vector2Int gridSize)
        {
            return slot.x >= _edgeColumnsKeptClear && slot.x < gridSize.x - _edgeColumnsKeptClear
                && slot.y >= _topRowsKeptClear && slot.y < gridSize.y - _bottomRowsKeptClear;
        }
    }
}
