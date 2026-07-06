namespace BalloonParty.Configuration.Ranges
{
    /// <summary>How a <see cref="RangedInt" />/<see cref="RangedFloat" /> scalar resolves to a concrete value.</summary>
    public enum RangeMode
    {
        Fixed,
        Linear,
        Random
    }
}
