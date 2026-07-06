using System;
using BalloonParty.Shared.Messages;

namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Uniquely identifies a score trail by color and its score value within
    ///     the level. Two different colors can share the same numeric score, so
    ///     both components are needed. No level component: the level-up is gated
    ///     by the transition, so a trail is only ever in flight during the single
    ///     level it belongs to — color+score never collides across levels.
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

        internal TrailId(ScorePointMessage msg)
        {
            Color = msg.ColorName;
            Score = msg.Score;
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
