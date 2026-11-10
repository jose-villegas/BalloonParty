using System;
using UnityEngine;

namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>
    ///     How one kind of heads-up reads and when it shows: its wording, its look, how it competes for a place
    ///     on screen, and the range of its argument worth writing.
    /// </summary>
    [Serializable]
    internal sealed class HeadsUpStyleSettings
    {
        [SerializeField] private HeadsUpStyleId _id;

        [Tooltip("Rich text. {0} is the event's number, or its word for a style that takes one (a colour's name).")]
        [SerializeField] private string _format;

        [SerializeField] private HeadsUpStyle _look;

        [Tooltip("When the screen is full, a heads-up evicts the oldest of the lowest priority showing, if that " +
                 "is no higher than its own; otherwise it is dropped.")]
        [SerializeField] private int _priority;

        [Tooltip("Seconds after one shows before another of this kind may. A merge rewrite ignores it.")]
        [SerializeField] [Min(0f)] private float _cooldownSeconds;

        [Tooltip("Seconds the written line stays before it fades.")]
        [SerializeField] [Min(0f)] private float _holdSeconds = 0.35f;

        [Tooltip("Held until released (the cannon's ready), in a lane of its own outside the cap.")]
        [SerializeField] private bool _held;

        [Tooltip("World units from the anchor to the line's centre.")]
        [SerializeField] private Vector2 _worldOffset;

        [Tooltip("The smallest argument worth writing; 0 = no floor.")]
        [SerializeField] [Min(0)] private int _minArg;

        [Tooltip("The largest argument worth writing; 0 = no ceiling.")]
        [SerializeField] [Min(0)] private int _maxArg;

        [Tooltip("The argument written at the look's base size; it grows from there.")]
        [SerializeField] [Min(1)] private int _escalateFrom = 1;

        [Tooltip("From this argument up the line is underlined in pencil once it's written; 0 = never.")]
        [SerializeField] [Min(0)] private int _underlineFrom;

        public HeadsUpStyleId Id => _id;
        public string Format => _format;
        public HeadsUpStyle Look => _look;
        public int Priority => _priority;
        public float CooldownSeconds => _cooldownSeconds;
        public float HoldSeconds => _holdSeconds;
        public bool Held => _held;
        public Vector2 WorldOffset => _worldOffset;
        public int MinArg => _minArg;
        public int MaxArg => _maxArg;
        public int EscalateFrom => _escalateFrom;
        public int UnderlineFrom => _underlineFrom;

        internal HeadsUpStyleSettings(HeadsUpStyleId id, string format, HeadsUpStyle look, int priority)
        {
            _id = id;
            _format = format;
            _look = look;
            _priority = priority;
        }

        /// <summary>Whether <paramref name="arg" /> is inside this style's range.</summary>
        public bool Accepts(int arg)
        {
            return (_minArg <= 0 || arg >= _minArg) && (_maxArg <= 0 || arg <= _maxArg);
        }

        /// <summary>The look's size for <paramref name="arg" />, counting from <see cref="EscalateFrom" />.</summary>
        public float SizeAt(int arg)
        {
            return _look.SizeAt(arg - _escalateFrom + 1);
        }

        /// <summary>Whether a line written for <paramref name="arg" /> gets an underline.</summary>
        public bool UnderlinesAt(int arg)
        {
            return _underlineFrom > 0 && arg >= _underlineFrom;
        }

        internal HeadsUpStyleSettings WithCooldown(float seconds)
        {
            _cooldownSeconds = seconds;
            return this;
        }

        internal HeadsUpStyleSettings WithHold(float seconds)
        {
            _holdSeconds = seconds;
            return this;
        }

        internal HeadsUpStyleSettings HeldUntilReleased()
        {
            _held = true;
            return this;
        }

        internal HeadsUpStyleSettings WithOffset(float x, float y)
        {
            _worldOffset = new Vector2(x, y);
            return this;
        }

        internal HeadsUpStyleSettings WithArgRange(int min, int max)
        {
            _minArg = min;
            _maxArg = max;
            return this;
        }

        internal HeadsUpStyleSettings EscalatingFrom(int arg)
        {
            _escalateFrom = arg;
            return this;
        }

        internal HeadsUpStyleSettings UnderlinedFrom(int arg)
        {
            _underlineFrom = arg;
            return this;
        }
    }
}
