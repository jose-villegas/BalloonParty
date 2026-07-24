namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    ///     A structural actor that paint runs off without recolouring — tough, unbreakable, armored.
    ///     The Paint item still plays the drip on these (the paint visibly slides off and reveals the
    ///     unchanged body), but never commits a colour. Distinct from "not <see cref="IPaintable" />":
    ///     empty slots and non-balloon actors (gatekeeper, bushes) are simply ignored.
    /// </summary>
    public interface IResistsPaint
    {
    }
}
