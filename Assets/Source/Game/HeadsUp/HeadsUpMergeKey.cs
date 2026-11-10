using System;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     What makes two heads-ups of one style the same line: a showing line with the same style and key is
    ///     rewritten rather than joined by another (a ball's growing streak). The owner compares by reference and
    ///     may be null (a pop no tracked ball caused); the channel separates one owner's lines (a colour).
    /// </summary>
    internal readonly struct HeadsUpMergeKey : IEquatable<HeadsUpMergeKey>
    {
        public readonly object Owner;
        public readonly string Channel;
        public readonly bool IsSet;

        public HeadsUpMergeKey(object owner, string channel)
        {
            Owner = owner;
            Channel = channel;
            IsSet = true;
        }

        public bool Equals(HeadsUpMergeKey other)
        {
            return IsSet && other.IsSet && ReferenceEquals(Owner, other.Owner)
                   && string.Equals(Channel, other.Channel, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is HeadsUpMergeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Owner, Channel);
        }
    }
}
