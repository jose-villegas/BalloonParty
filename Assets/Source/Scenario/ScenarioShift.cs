using System;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     A switch of the backdrop to <see cref="Kind" />: a cut when <see cref="Seconds" /> is 0, otherwise the
    ///     mist reveal played over that many (unscaled) seconds, the length of the beat it rides on.
    /// </summary>
    internal readonly struct ScenarioShift : IEquatable<ScenarioShift>
    {
        public readonly ScenarioKind Kind;
        public readonly float Seconds;

        public bool IsCut => Seconds <= 0f;

        public ScenarioShift(ScenarioKind kind, float seconds)
        {
            Kind = kind;
            Seconds = Math.Max(0f, seconds);
        }

        public bool Equals(ScenarioShift other)
        {
            return Kind == other.Kind && Seconds.Equals(other.Seconds);
        }

        public override bool Equals(object obj)
        {
            return obj is ScenarioShift other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ Seconds.GetHashCode();
        }

        public override string ToString()
        {
            return IsCut ? $"cut to {Kind}" : $"{Kind} over {Seconds:0.##}s";
        }
    }
}
