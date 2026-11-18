namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A view binding that spins while its balloon is ripe (the soap bubble cluster). The spiral arm a ripe
    ///     cluster sheds (<see cref="RipeBubbleBurstView" />) reads the way round from it, so the arm and the cluster
    ///     turn together.
    /// </summary>
    internal interface IRipeSpin
    {
        /// <summary>The way the cluster turns on screen while ripe: +1 counter-clockwise, -1 clockwise.</summary>
        float TurnDirection { get; }
    }
}
