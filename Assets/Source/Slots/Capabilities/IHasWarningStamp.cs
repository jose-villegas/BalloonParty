namespace BalloonParty.Slots.Capabilities
{
    /// <summary>An actor that pulses a warning stamp into the disturbance field while on its last hit.</summary>
    public interface IHasWarningStamp
    {
        /// <summary>Seconds between warning stamps; 0 = no pulse.</summary>
        float WarningStampInterval { get; }
    }
}
