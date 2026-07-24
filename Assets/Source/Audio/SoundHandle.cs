using System;

namespace BalloonParty.Audio
{
    internal readonly struct SoundHandle : IEquatable<SoundHandle>
    {
        public static readonly SoundHandle None = default;

        private readonly int _voiceId;
        private readonly uint _generation;

        public int VoiceId => _voiceId;
        public uint Generation => _generation;
        public bool IsValid => _generation != 0u;

        public SoundHandle(int voiceId, uint generation)
        {
            _voiceId = voiceId;
            _generation = generation;
        }

        public bool Equals(SoundHandle other)
        {
            return _voiceId == other._voiceId && _generation == other._generation;
        }

        public override bool Equals(object obj)
        {
            return obj is SoundHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_voiceId, _generation);
        }

        public static bool operator ==(SoundHandle left, SoundHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SoundHandle left, SoundHandle right)
        {
            return !left.Equals(right);
        }
    }
}
