namespace BalloonParty.Game.Telemetry
{
    // The envelope discriminator (R24). Session is reserved: the scope accumulates (R1) but nothing
    // flushes it yet, so no Session envelope is ever emitted in the waves built so far.
    internal enum RecordKind
    {
        Flight,
        Level,
        Run,
        Session
    }
}
