namespace BalloonParty.Shared
{
    /// <summary>
    ///     A small seeded generator of whole numbers (xorshift32). The same seed gives the same sequence on every
    ///     device and runtime, because it is nothing but integer arithmetic written out here; a library generator
    ///     is free to change between versions. For rolls that must replay: a level laid out from its seed.
    /// </summary>
    internal struct XorShift32
    {
        // What a seed that scrambles to 0 starts from instead: xorshift never leaves 0.
        private const uint ZeroSeedState = 0x9E3779B9u;

        private uint _state;

        internal XorShift32(int seed)
        {
            // Murmur3's finaliser, so that seeds next to each other do not start alike.
            var state = unchecked((uint)seed);
            state ^= state >> 16;
            state = unchecked(state * 0x85EBCA6Bu);
            state ^= state >> 13;
            state = unchecked(state * 0xC2B2AE35u);
            state ^= state >> 16;
            _state = state == 0u ? ZeroSeedState : state;
        }

        /// <summary>The next number of the sequence, anywhere in the 32 bits but never 0.</summary>
        internal uint NextUInt()
        {
            var state = _state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _state = state;
            return state;
        }

        /// <summary>The next number of the sequence scaled into 0 up to, not including,
        /// <paramref name="exclusiveMax" />; 0 when that is 1 or less.</summary>
        internal int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 1)
            {
                return 0;
            }

            return (int)(((ulong)NextUInt() * (ulong)exclusiveMax) >> 32);
        }
    }
}
