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

        [Tooltip("The row the tough snake keeps to, counted from the top (0 is the top row). It climbs toward it, " +
                 "and on it walks only left and right, so once its body is up the whole snake slides along it.")]
        [SerializeField] [Min(0)] private int _toughSnakeRow = 1;

        [Tooltip("A formation picks at random among this many of its least-travel spots, so it doesn't " +
                 "always form in the same place. 1 = always the least travel.")]
        [SerializeField] [Min(1)] private int _spotChoices = 3;

        [Tooltip("How strongly a cover prefers a gift balloon (golden, heart, disco ball, nuke) to a balloon " +
                 "carrying a bomb, a snipe or a lightning. In slots of travel for each member: a spot costs the " +
                 "slots its members travel to it, plus 3 for each balloon they move out of the way, and a gift's " +
                 "spot counts as this many slots cheaper for each member. At 2, three unbreakables cover the " +
                 "gift unless covering the item carrier would cost over 6 slots less. 0 = no preference: the " +
                 "cheaper spot wins.")]
        [SerializeField] [Min(0f)] private float _giftCoverPreference = 2f;

        [Header("Clock")]
        [Tooltip("Seconds of play between the unbreakables' beats. An unbreakable snake steps Snake Steps Per " +
                 "Beat times a beat, and bubble clusters merge on its off-beat. A pause holds the clock; a doomed shot " +
                 "does not.")]
        [FormerlySerializedAs("_snakeStepSeconds")]
        [SerializeField] [Min(0.1f)] private float _beatSeconds = 1.7f;

        [Tooltip("Beats in a cycle, the clock the unbreakables keep together: one lone unbreakable at most " +
                 "moves on each, and every Reshape Every Cycles of them the formations form and change shape.")]
        [SerializeField] [Min(1)] private int _beatsPerCycle = 2;

        [Tooltip("Cycles between the board's reshapes. On a reshape every formation changes shape at once and the " +
                 "free unbreakables form up; in between every formation holds its shape, whatever it is.")]
        [SerializeField] [Min(1)] private int _reshapeEveryCycles = 3;

        [Tooltip("Steps an unbreakable snake takes each beat, evenly spaced: 2 = on the beat and the off-beat, " +
                 "so it walks quicker than the tough snake while keeping the unbreakables' rhythm.")]
        [SerializeField] [Min(1)] private int _snakeStepsPerBeat = 2;

        [Tooltip("Cycles a lone unbreakable, in no formation, stays where it is before it may move again. One " +
                 "at most moves alone on each cycle.")]
        [SerializeField] [Min(1)] private int _loneHoldCycles = 3;

        [Header("Ring")]
        [Tooltip("Fewest unbreakables a ring forms from. A ring stands round one slot and turns one slot round it " +
                 "on every beat.")]
        [SerializeField] [Range(2, 5)] private int _ringMinMembers = 4;

        [Tooltip("Most unbreakables in a ring. Five at most: of the six slots round its centre one always stays " +
                 "open.")]
        [SerializeField] [Range(2, 5)] private int _ringMaxMembers = 5;

        [Tooltip("Cycles between the beats on which every ring and every spinner turns the other way. It is one " +
                 "beat for the whole board, so they all turn back together. 0 = they never turn back by " +
                 "themselves.")]
        [SerializeField] [Min(0)] private int _turnReverseEveryCycles = 4;

        [Header("Spinner")]
        [Tooltip("Most unbreakables in a spinner, a straight line that turns 60 degrees about the member in its " +
                 "middle on every beat: 3 or 5.")]
        [SerializeField] [Range(3, 5)] private int _spinnerMaxMembers = 5;

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

        [Tooltip("Weight of a cover. It finds a spot only while a balloon carrying a bomb, a snipe or a " +
                 "lightning, or a gift balloon (golden, heart, disco ball, nuke), is on the board.")]
        [SerializeField] [Min(0f)] private float _itemCoverWeight = 2f;

        [Tooltip("Weight of a snake, the line that walks the board.")]
        [SerializeField] [Min(0f)] private float _snakeWeight = 0.25f;

        [Tooltip("Weight of a ring. It forms only from Ring Min Members unbreakables or more.")]
        [SerializeField] [Min(0f)] private float _ringWeight = 1f;

        [Tooltip("Weight of a spinner, the line that turns about its middle. It forms from three unbreakables " +
                 "or more.")]
        [SerializeField] [Min(0f)] private float _spinnerWeight = 1f;

        [Tooltip("How many times likelier a ring is for a snake done walking: the snake coils into it, its head " +
                 "leading. 1 = no likelier than for any other formation.")]
        [SerializeField] [Min(0f)] private float _ringWeightAfterSnake = 4f;

        [Tooltip("Added to the snake's weight, as a share of it, for each unbreakable on the board beyond Min " +
                 "Members: 0.25 makes a snake 1.25x as likely with one more, 1.5x with two more.")]
        [SerializeField] [Min(0f)] private float _snakeWeightPerExtraUnbreakable = 0.25f;

        [Tooltip("Most unbreakable snakes on the board at once; a formation that would make one more picks another " +
                 "shape. 0 sets no cap.")]
        [SerializeField] [Min(0)] private int _maxUnbreakableSnakes = 2;

        [Tooltip("Chance that each member of a formation changing shape on its cycle leaves it and flies to an " +
                 "unbreakable snake's tail instead, while that snake has room (Max Members).")]
        [SerializeField] [Range(0f, 1f)] private float _joinSnakeChance = 0.35f;

        public bool Enabled => _enabled;
        public bool ToughSnakes => _toughSnakes;
        public int MinMembers => _minMembers;
        public int MaxMembers => Mathf.Max(_maxMembers, _minMembers);
        public float BeatSeconds => Mathf.Max(0.1f, _beatSeconds);
        public int BeatsPerCycle => Mathf.Max(1, _beatsPerCycle);
        public int SnakeStepsPerBeat => Mathf.Max(1, _snakeStepsPerBeat);
        public int ReshapeEveryCycles => Mathf.Max(1, _reshapeEveryCycles);
        public int LoneHoldCycles => Mathf.Max(1, _loneHoldCycles);
        public int BottomClearance => _bottomClearance;
        public int HorizontalBottomClearance => Mathf.Max(_horizontalBottomClearance, _bottomClearance);
        public int HorizontalMaxMembers => Mathf.Clamp(_horizontalMaxMembers, _minMembers, MaxMembers);
        public int SnakeMaxMembers => Mathf.Clamp(_snakeMaxMembers, _minMembers, MaxMembers);
        public float ToughSnakeStepSeconds => Mathf.Max(0.1f, _toughSnakeStepSeconds);
        public int ToughSnakeMinMembers => Mathf.Max(1, _toughSnakeMinMembers);
        public int ToughSnakeRow => Mathf.Max(0, _toughSnakeRow);
        public int SpotChoices => _spotChoices;
        public float GiftCoverPreference => Mathf.Max(0f, _giftCoverPreference);
        public float HorizontalWeight => _horizontalWeight;
        public float DiagonalWeight => _diagonalWeight;
        public float ItemCoverWeight => _itemCoverWeight;
        public float SnakeWeight => _snakeWeight;
        public float RingWeight => _ringWeight;
        public float RingWeightAfterSnake => _ringWeightAfterSnake;
        public int RingMinMembers => _ringMinMembers;
        public int RingMaxMembers => Mathf.Max(_ringMaxMembers, _ringMinMembers);
        public int TurnReverseEveryCycles => Mathf.Max(0, _turnReverseEveryCycles);
        public float SpinnerWeight => _spinnerWeight;
        public int SpinnerMaxMembers => _spinnerMaxMembers;
        public float SnakeWeightPerExtraUnbreakable => _snakeWeightPerExtraUnbreakable;
        public int MaxUnbreakableSnakes => _maxUnbreakableSnakes;
        public float JoinSnakeChance => _joinSnakeChance;
        public float ReinforcementChance => _reinforcementChance;
        public int ReinforcementCeiling => _reinforcementCeiling;
    }
}
