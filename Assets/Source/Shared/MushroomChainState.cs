namespace BalloonParty.Shared
{
    /// <summary>
    ///     One flight's tally of mushroom bounces, which <see cref="MushroomChainRule" /> reads and writes: how many
    ///     hits count since the flight began or last touched a wall of the board, how many times it has bounced off
    ///     a cap in that stretch, and which wall of caps was hit last. The default value is a flight that has hit
    ///     nothing.
    /// </summary>
    /// <remarks>
    ///     It rides on the ball's flight and on the shot solver's simulated flight alike, so it is a plain value with
    ///     no model behind it.
    /// </remarks>
    internal struct MushroomChainState
    {
        /// <summary>No cap: what a caller passes for the cap a ball is leaving when it is leaving none.</summary>
        internal const int NoBumper = -1;

        /// <summary>What <see cref="LastWall" /> reads when no cap has been hit since the board's last wall.</summary>
        internal const int NoWall = -1;

        /// <summary>Hits counted since the flight began or last touched a wall of the board: one a wall of caps,
        /// and not the same wall twice in a row.</summary>
        public int Count;

        /// <summary>
        ///     Every bounce off a cap since the flight began or last touched a wall of the board, the ones that did
        ///     not count included (the same wall of caps twice in a row). It is what the cap on bounces is held against
        ///     (<see cref="MushroomChainRules.MaxBouncesPerStretch" />).
        /// </summary>
        public int Bounces;

        // Kept one above the wall, so the default state has no last wall although walls start at 0.
        private int _lastWallPlusOne;

        /// <summary>The wall of caps hit last (<see cref="BumperCircle.Wall" />), or <see cref="NoWall" />.</summary>
        public int LastWall => _lastWallPlusOne - 1;

        internal void RememberLast(int wall)
        {
            _lastWallPlusOne = wall + 1;
        }

        internal void ForgetLast()
        {
            _lastWallPlusOne = 0;
        }
    }
}
