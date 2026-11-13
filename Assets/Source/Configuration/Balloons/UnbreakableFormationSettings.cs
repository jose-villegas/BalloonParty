using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class UnbreakableFormationSettings : IUnbreakableFormationSettings
    {
        [Tooltip("Off: unbreakables only roam, as before formations.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("Fewest unbreakables on the board that gather into a formation.")]
        [SerializeField] [Min(2)] private int _minMembers = 3;

        [Tooltip("Most unbreakables in one formation; the rest roam, or form a formation of their own.")]
        [SerializeField] [Min(2)] private int _maxMembers = 5;

        [Tooltip("Waves a formation holds, counting the wave it formed in, before it changes shape.")]
        [SerializeField] [Min(1)] private int _holdWaves = 3;

        [Tooltip("Bottom rows a formation never uses, so spawn lines always have room to enter.")]
        [SerializeField] [Min(0)] private int _bottomClearance = 2;

        [Tooltip("Bottom rows a horizontal line never uses, so a wall " +
                 "across the board stands well above the cannon rather than walling it in; diagonals leave lanes " +
                 "open and keep Bottom Clearance. Never less than Bottom Clearance.")]
        [SerializeField] [Min(0)] private int _horizontalBottomClearance = 4;

        [Tooltip("Most unbreakables in a horizontal line, so a row never spans " +
                 "the board; the rest are free to form their own. Held between Min Members and Max Members.")]
        [SerializeField] [Min(2)] private int _horizontalMaxMembers = 3;

        [Tooltip("Most unbreakables in a snake, the line that walks the board. Held between Min Members and " +
                 "Max Members.")]
        [SerializeField] [Min(2)] private int _snakeMaxMembers = 4;

        [Tooltip("Balancer pulses (Flight Rebalance Interval) between a snake's steps. Every step lands on the " +
                 "board's own beat, so the player can see the next one coming.")]
        [SerializeField] [Min(1)] private int _snakeStepPulses = 1;

        [Tooltip("A formation picks at random among this many of its least-travel spots, so it doesn't " +
                 "always form in the same place. 1 = always the least travel.")]
        [SerializeField] [Min(1)] private int _spotChoices = 3;

        [Header("Reinforcements")]
        [Tooltip("Chance, at each wave start, that a formation which stood untouched through the last wave " +
                 "(no member pierced since it formed) calls in one more unbreakable. 0 = never.")]
        [SerializeField] [Range(0f, 1f)] private float _reinforcementChance = 0.35f;

        [Tooltip("Most called-in unbreakables on the board at once, on top of the level's own cap. They roam " +
                 "and form their own formations like any other.")]
        [SerializeField] [Min(0)] private int _reinforcementCeiling = 4;

        [Header("Shape weights")]
        [SerializeField] [Min(0f)] private float _horizontalWeight = 1f;

        [Tooltip("Weight of each of the two diagonals.")]
        [SerializeField] [Min(0f)] private float _diagonalWeight = 1f;

        [Tooltip("Weight of ringing an item. Only in the pick while a balloon on the board carries an item.")]
        [SerializeField] [Min(0f)] private float _itemCoverWeight = 2f;

        [Tooltip("Weight of a snake, the line that walks the board.")]
        [SerializeField] [Min(0f)] private float _snakeWeight = 1f;

        public bool Enabled => _enabled;
        public int MinMembers => _minMembers;
        public int MaxMembers => Mathf.Max(_maxMembers, _minMembers);
        public int HoldWaves => _holdWaves;
        public int BottomClearance => _bottomClearance;
        public int HorizontalBottomClearance => Mathf.Max(_horizontalBottomClearance, _bottomClearance);
        public int HorizontalMaxMembers => Mathf.Clamp(_horizontalMaxMembers, _minMembers, MaxMembers);
        public int SnakeMaxMembers => Mathf.Clamp(_snakeMaxMembers, _minMembers, MaxMembers);
        public int SnakeStepPulses => Mathf.Max(1, _snakeStepPulses);
        public int SpotChoices => _spotChoices;
        public float HorizontalWeight => _horizontalWeight;
        public float DiagonalWeight => _diagonalWeight;
        public float ItemCoverWeight => _itemCoverWeight;
        public float SnakeWeight => _snakeWeight;
        public float ReinforcementChance => _reinforcementChance;
        public int ReinforcementCeiling => _reinforcementCeiling;
    }
}
