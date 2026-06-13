namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Broadcast on a run restart. Every actor that owns a pooled view (balloons, static
    ///     actors) returns itself to its pool and vacates its grid slot when it receives this,
    ///     so the board-clear honours the "the consumer that Get()s it Return()s it" contract.
    /// </summary>
    public readonly struct BoardClearMessage
    {
    }
}
