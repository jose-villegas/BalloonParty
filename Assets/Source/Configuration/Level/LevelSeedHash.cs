namespace BalloonParty.Configuration.Level
{
    /// <summary>The one hash every per-level roll uses, so a retry replays the same level.</summary>
    internal static class LevelSeedHash
    {
        /// <summary>A non-negative hash of <paramref name="value" /> and <paramref name="salt" />.</summary>
        public static int Hash(int value, int salt)
        {
            unchecked
            {
                // Murmur3's finaliser: neighbouring values get unrelated hashes.
                var h = (uint)value * 0x9E3779B1u ^ (uint)salt;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
