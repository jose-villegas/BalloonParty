using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class UnbreakableFormationSettings : IUnbreakableFormationSettings
    {
        [Tooltip("Off: unbreakables only roam, as before formations, and toughs never snake.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("The board's toughs (and toughers) walk as one snake of their own, through the balloons in its " +
                 "way, keeping its order. Once Tough Snake Min Members toughs are on the board it starts " +
                 "from the biggest group with the rest lined up behind it at once, and each tough that lands later falls in " +
                 "behind its tail as soon as it lands.")]
        [SerializeField] private bool _toughSnakes = true;

        [Tooltip("Fewest unbreakables on the board that gather into a formation.")]
        [SerializeField] [Min(2)] private int _minMembers = 2;

        [Tooltip("Most unbreakables in one formation; the rest roam, or form a formation of their own.")]
        [SerializeField] [Min(2)] private int _maxMembers = 5;

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

        [Tooltip("Seconds of play between the tough snake's steps, on a clock of its own apart from the beat. A " +
                 "pause or a doomed shot holds it.")]
        [SerializeField] [Min(0.1f)] private float _toughSnakeStepSeconds = 1.5f;

        [Tooltip("Fewest toughs (and toughers) on the board that start the tough snake. Apart from Min Members, " +
                 "which the unbreakables' formations keep, so a pair of toughers walks as one too.")]
        [SerializeField] [Min(1)] private int _toughSnakeMinMembers = 2;

        [Tooltip("A formation picks at random among this many of its least-travel spots, so it doesn't " +
                 "always form in the same place. 1 = always the least travel.")]
        [SerializeField] [Min(1)] private int _spotChoices = 3;

        [Header("Clock")]
        [Tooltip("Seconds of play between the unbreakables' beats. An unbreakable snake steps Snake Steps Per " +
                 "Beat times a beat. A pause or a doomed shot holds the clock.")]
        [FormerlySerializedAs("_snakeStepSeconds")]
        [SerializeField] [Min(0.1f)] private float _beatSeconds = 1.7f;

        [Tooltip("Beats in a cycle, the clock the unbreakables keep together: formations form and change shape " +
                 "only on a cycle, and one lone unbreakable at most moves on each.")]
        [SerializeField] [Min(1)] private int _beatsPerCycle = 2;

        [Tooltip("Steps an unbreakable snake takes each beat, evenly spaced: 2 = on the beat and the off-beat, " +
                 "so it walks quicker than the tough snake while keeping the unbreakables' rhythm.")]
        [SerializeField] [Min(1)] private int _snakeStepsPerBeat = 2;

        [Tooltip("Cycles a snake walks, counting the cycle it formed on, before it changes shape.")]
        [SerializeField] [Min(1)] private int _snakeHoldCycles = 3;

        [Tooltip("Cycles a horizontal or diagonal line holds, counting the cycle it formed on, before it changes " +
                 "shape.")]
        [SerializeField] [Min(1)] private int _lineHoldCycles = 4;

        [Tooltip("Cycles an item cover holds, counting the cycle it formed on, before it changes shape.")]
        [SerializeField] [Min(1)] private int _itemCoverHoldCycles = 4;

        [Tooltip("Cycles a lone unbreakable, in no formation, stays where it is before it may move again. One " +
                 "at most moves alone on each cycle.")]
        [SerializeField] [Min(1)] private int _loneHoldCycles = 3;

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

        [Tooltip("Added to the snake's weight, as a share of it, for each unbreakable on the board beyond Min " +
                 "Members: 0.5 makes a snake 1.5x as likely with one more, 2x with two more.")]
        [SerializeField] [Min(0f)] private float _snakeWeightPerExtraUnbreakable = 0.5f;

        public bool Enabled => _enabled;
        public bool ToughSnakes => _toughSnakes;
        public int MinMembers => _minMembers;
        public int MaxMembers => Mathf.Max(_maxMembers, _minMembers);
        public float BeatSeconds => Mathf.Max(0.1f, _beatSeconds);
        public int BeatsPerCycle => Mathf.Max(1, _beatsPerCycle);
        public int SnakeStepsPerBeat => Mathf.Max(1, _snakeStepsPerBeat);
        public int SnakeHoldCycles => Mathf.Max(1, _snakeHoldCycles);
        public int LineHoldCycles => Mathf.Max(1, _lineHoldCycles);
        public int ItemCoverHoldCycles => Mathf.Max(1, _itemCoverHoldCycles);
        public int LoneHoldCycles => Mathf.Max(1, _loneHoldCycles);
        public int BottomClearance => _bottomClearance;
        public int HorizontalBottomClearance => Mathf.Max(_horizontalBottomClearance, _bottomClearance);
        public int HorizontalMaxMembers => Mathf.Clamp(_horizontalMaxMembers, _minMembers, MaxMembers);
        public int SnakeMaxMembers => Mathf.Clamp(_snakeMaxMembers, _minMembers, MaxMembers);
        public float ToughSnakeStepSeconds => Mathf.Max(0.1f, _toughSnakeStepSeconds);
        public int ToughSnakeMinMembers => Mathf.Max(1, _toughSnakeMinMembers);
        public int SpotChoices => _spotChoices;
        public float HorizontalWeight => _horizontalWeight;
        public float DiagonalWeight => _diagonalWeight;
        public float ItemCoverWeight => _itemCoverWeight;
        public float SnakeWeight => _snakeWeight;
        public float SnakeWeightPerExtraUnbreakable => _snakeWeightPerExtraUnbreakable;
        public float ReinforcementChance => _reinforcementChance;
        public int ReinforcementCeiling => _reinforcementCeiling;
    }
}
