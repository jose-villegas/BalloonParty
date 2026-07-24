using System;

namespace BalloonParty.Audio
{
    /// <summary>
    ///     Identifies one play of a voice slot — the only way to <see cref="ISoundPlayer.Stop"/> a
    ///     loop. Pairs a voice-slot index with a generation counter that <c>SfxService</c> bumps
    ///     every time it (re)plays that slot, including when <c>VoiceLimiter</c> steals the slot out
    ///     from under an in-progress sound. <see cref="ISoundPlayer.Stop"/> compares the handle's
    ///     generation against the slot's current one and no-ops on a mismatch, so a caller holding a
    ///     stale handle (its sound was stolen by something higher-priority before it called Stop)
    ///     can never tear down whatever unrelated sound now occupies that slot.
    /// </summary>
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
