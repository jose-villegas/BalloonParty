using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     The spores a meadow mushroom lets go (@ref plan_meadow_mushrooms): a puff of smoke and a few specks
    ///     when a ball hits its cap, a little more when a lance cuts it, and a burst when a blast blows it off.
    ///     Authored on the mushroom settings (<c>IMushroomSettings.Spores</c>).
    /// </summary>
    /// <remarks>
    ///     The look only. The smoke is the game's smoke field, so its colour and how long it lingers are that
    ///     field's (its <c>MushroomSpores</c> profile and its decay); these numbers say where it is put and how
    ///     much of it. The specks are the mushroom's own particles.
    /// </remarks>
    [Serializable]
    internal struct MushroomSporeTuning
    {
        [Header("How often")]
        [Tooltip("Seconds a mushroom keeps quiet after it has puffed: a ball can knock a cap every 80 ms in a " +
            "rally, and that should read as a few puffs, not a fog.")]
        [SerializeField] [Range(0f, 2f)] private float _refractory;

        [Tooltip("What the whole board may let go at once, counted in hits: a hit takes 1 of it, a cut and a " +
            "blast more. When less is left a puff is that much smaller, so seven caps blown off by a nuke do " +
            "not fog the board.")]
        [SerializeField] [Min(1f)] private float _budget;

        [Tooltip("How much of that comes back each second.")]
        [SerializeField] [Min(0.1f)] private float _budgetPerSecond;

        [Header("A hit")]
        [Tooltip("The ball's speed under which a hit lets nothing go, in world units a second.")]
        [SerializeField] [Min(0f)] private float _minSpeed;

        [Tooltip("The ball's speed at which a hit gives the whole puff. The ball leaves the cannon at about 10.")]
        [SerializeField] [Min(0.1f)] private float _fullSpeed;

        [Tooltip("How long the puff's cone is at full speed, in world units, from the cap's far rim along the " +
            "ball's way. A cap is 0.6 across.")]
        [SerializeField] [Min(0f)] private float _length;

        [Tooltip("How wide the cone is at its far end, in world units.")]
        [SerializeField] [Min(0f)] private float _endWidth;

        [Tooltip("How thick the smoke is at full speed, as a share of the smoke profile's own opacity.")]
        [SerializeField] [Range(0f, 1f)] private float _smoke;

        [Tooltip("Specks at full speed.")]
        [SerializeField] [Range(0, 24)] private int _specks;

        [Tooltip("How much more the hit that pays a chain lets go: 1.5 is half as much again.")]
        [SerializeField] [Range(1f, 3f)] private float _paidScale;

        [Header("A cut and a blast")]
        [Tooltip("A lance's cut, as a multiple of a full hit: a little dust and a few specks along the lance's " +
            "way as the cap opens. Well under the blast's, so the three read as steps.")]
        [SerializeField] [Range(0f, 4f)] private float _cutScale;

        [Tooltip("A cap blown off, as a multiple of a full hit: the biggest moment.")]
        [SerializeField] [Range(0f, 8f)] private float _blastScale;

        [Tooltip("Seconds after the blast before its burst is let go. A bomb's own cloud covers the mushroom " +
            "for about 0.4 seconds; the burst is for when it thins.")]
        [SerializeField] [Range(0f, 1f)] private float _blastDelay;

        [Tooltip("How wide a blast's specks fan out, in degrees about the way the cap is blown. A hit's stay " +
            "inside its cone.")]
        [SerializeField] [Range(10f, 360f)] private float _blastFanDegrees;

        [Header("The specks")]
        [Tooltip("How fast a speck leaves at full speed, in world units a second. It slows as it goes.")]
        [SerializeField] [Min(0f)] private float _speckSpeed;

        [Tooltip("How long a speck lives, in seconds, give or take a third.")]
        [SerializeField] [Range(0.1f, 3f)] private float _speckSeconds;

        [Tooltip("A speck's size, in world units.")]
        [SerializeField] [Range(0.01f, 0.3f)] private float _speckSize;

        [Tooltip("The specks' colours: each is somewhere between the two.")]
        [SerializeField] private Color _speckGreen;
        [SerializeField] private Color _speckBlue;

        public float Refractory => Mathf.Max(0f, _refractory);
        public float Budget => Mathf.Max(1f, _budget);
        public float BudgetPerSecond => Mathf.Max(0.1f, _budgetPerSecond);
        public float MinSpeed => Mathf.Max(0f, _minSpeed);
        public float FullSpeed => Mathf.Max(MinSpeed + 0.1f, _fullSpeed);
        public float Length => Mathf.Max(0f, _length);
        public float EndWidth => Mathf.Max(0f, _endWidth);
        public float Smoke => Mathf.Clamp01(_smoke);
        public int Specks => Mathf.Max(0, _specks);
        public float PaidScale => Mathf.Max(1f, _paidScale);
        public float CutScale => Mathf.Max(0f, _cutScale);
        public float BlastScale => Mathf.Max(0f, _blastScale);
        public float BlastDelay => Mathf.Max(0f, _blastDelay);
        public float BlastFanDegrees => Mathf.Clamp(_blastFanDegrees, 10f, 360f);
        public float SpeckSpeed => Mathf.Max(0f, _speckSpeed);
        public float SpeckSeconds => Mathf.Max(0.1f, _speckSeconds);
        public float SpeckSize => Mathf.Max(0.01f, _speckSize);
        public Color SpeckGreen => _speckGreen;
        public Color SpeckBlue => _speckBlue;

        /// <summary>The cone's opening, in degrees: what its length and its width at the far end make it.</summary>
        public float ConeDegrees => 2f * Mathf.Atan2(EndWidth * 0.5f, Mathf.Max(Length, 1e-4f)) * Mathf.Rad2Deg;

        internal MushroomSporeTuning(
            float refractory, float budget, float budgetPerSecond, in MushroomSporeHit hit, float cutScale,
            float blastScale, float blastDelay, float blastFanDegrees, in MushroomSporeSpecks specks)
        {
            _refractory = refractory;
            _budget = budget;
            _budgetPerSecond = budgetPerSecond;
            _minSpeed = hit.MinSpeed;
            _fullSpeed = hit.FullSpeed;
            _length = hit.Length;
            _endWidth = hit.EndWidth;
            _smoke = hit.Smoke;
            _specks = hit.Specks;
            _paidScale = hit.PaidScale;
            _cutScale = cutScale;
            _blastScale = blastScale;
            _blastDelay = blastDelay;
            _blastFanDegrees = blastFanDegrees;
            _speckSpeed = specks.Speed;
            _speckSeconds = specks.Seconds;
            _speckSize = specks.Size;
            _speckGreen = specks.Green;
            _speckBlue = specks.Blue;
        }
    }

    /// <summary>A hit's puff at full speed, for building a <see cref="MushroomSporeTuning" />.</summary>
    internal readonly struct MushroomSporeHit
    {
        public readonly float MinSpeed;
        public readonly float FullSpeed;
        public readonly float Length;
        public readonly float EndWidth;
        public readonly float Smoke;
        public readonly int Specks;
        public readonly float PaidScale;

        internal MushroomSporeHit(
            float minSpeed, float fullSpeed, float length, float endWidth, float smoke, int specks, float paidScale)
        {
            MinSpeed = minSpeed;
            FullSpeed = fullSpeed;
            Length = length;
            EndWidth = endWidth;
            Smoke = smoke;
            Specks = specks;
            PaidScale = paidScale;
        }
    }

    /// <summary>The specks' own numbers, for building a <see cref="MushroomSporeTuning" />.</summary>
    internal readonly struct MushroomSporeSpecks
    {
        public readonly float Speed;
        public readonly float Seconds;
        public readonly float Size;
        public readonly Color Green;
        public readonly Color Blue;

        internal MushroomSporeSpecks(float speed, float seconds, float size, Color green, Color blue)
        {
            Speed = speed;
            Seconds = seconds;
            Size = size;
            Green = green;
            Blue = blue;
        }
    }
}
