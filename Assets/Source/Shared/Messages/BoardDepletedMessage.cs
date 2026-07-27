namespace BalloonParty.Shared.Messages
{
    /// <summary>Broadcast when the last active balloon is organically destroyed during gameplay
    /// (not an administrative board-clear). General-purpose — listeners may use it for audio cues,
    /// VFX, or the level-up ceremony's early-finish path.</summary>
    public readonly struct BoardDepletedMessage
    {
    }
}
