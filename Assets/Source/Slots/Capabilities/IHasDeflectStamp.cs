namespace BalloonParty.Slots.Capabilities
{
    /// <summary>An actor whose projectile deflect stamps the disturbance field at its slot — heavy types jolt the air on impact.</summary>
    public interface IHasDeflectStamp
    {
        /// <summary>Multiplier on the BalloonDeflect stamp profile's radius; 0 = no stamp.</summary>
        float DeflectStampScale { get; }
    }
}
