using System;

namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Uniquely identifies a score trail by color and score value; no level component since a
    ///     trail is only ever in flight within the single level it belongs to.
    /// </summary>
    internal readonly struct TrailId : IEquatable<TrailId>
    {
        internal readonly string Color;
        internal readonly int Score;

        internal TrailId(string color, int score)
        {
            Color = color;
            Score = score;
        }

        public bool Equals(TrailId other)
        {
            return Color == other.Color && Score == other.Score;
        }

        public override bool Equals(object obj)
        {
            return obj is TrailId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Color, Score);
        }

        public override string ToString()
        {
            return $"{Color}:{Score}";
        }

        public static bool operator ==(TrailId left, TrailId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TrailId left, TrailId right)
        {
            return !left.Equals(right);
        }
    }
}
