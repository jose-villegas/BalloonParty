using System;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     One showing heads-up: its slot in the queue and that slot's generation, so a handle kept past its line
    ///     (a late "finished", a release after a clear) never touches the line now in the slot.
    /// </summary>
    internal readonly struct HeadsUpHandle : IEquatable<HeadsUpHandle>
    {
        public static readonly HeadsUpHandle None = new(-1, 0);

        public readonly int Slot;
        public readonly int Generation;

        public bool IsValid => Slot >= 0;

        public HeadsUpHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(HeadsUpHandle other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is HeadsUpHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Slot * 397) ^ Generation;
        }

        public static bool operator ==(HeadsUpHandle left, HeadsUpHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HeadsUpHandle left, HeadsUpHandle right)
        {
            return !left.Equals(right);
        }
    }
}
