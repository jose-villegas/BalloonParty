namespace BalloonParty.Shared.Thermal
{
    /// <summary>
    ///     Always-cool <see cref="IThermalSource" /> for the editor and non-Android platforms, where no
    ///     thermal API exists. Keeps the governor pinned to its top rung.
    /// </summary>
    internal sealed class StubThermalSource : IThermalSource
    {
        public float Headroom => 0f;
        public int Status => 0;
    }
}
