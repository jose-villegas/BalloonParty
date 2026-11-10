namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>
    ///     Each kind of heads-up, keying its entry in <see cref="IHeadsUpSettings" />. The numbers are stored in the
    ///     settings asset: never renumber or reuse one.
    /// </summary>
    internal enum HeadsUpStyleId : byte
    {
        Ready = 0,
        BouncePickup = 1,
        BounceWall = 2,
        BounceWallLast = 3,
        Ricochet = 4,
        RicochetPaid = 5,
        Streak = 6,
        SpeedTap = 7,
        Pierce = 8,
        Shattered = 9,
        HeartLost = 10,
        ColorDone = 11,
        Rainbow = 12
    }
}
