using System;
using BalloonParty.Shared.Messages;

namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Uniquely identifies a score trail by color, score value within the
    ///     level, and the level it was spawned during. Two different colors can
    ///     share the same numeric score, and after a level reset scores restart
    ///     from 1 — so all three components are needed for uniqueness.
    /// </summary>
    internal readonly struct TrailId : IEquatable<TrailId>
    {
        internal readonly string Color;
        internal readonly int Score;
        internal readonly int Level;

        internal TrailId(string color, int score, int level)
        {
            Color = color;
            Score = score;
            Level = level;
        }

        internal TrailId(ScorePointMessage msg)
        {
            Color = msg.ColorName;
            Score = msg.Score;
            Level = msg.Level;
        }

        public bool Equals(TrailId other)
        {
            return Color == other.Color && Score == other.Score && Level == other.Level;
        }

        public override bool Equals(object obj)
        {
            return obj is TrailId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Color, Score, Level);
        }

        public override string ToString()
        {
            return $"{Color}:{Score}@L{Level}";
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

