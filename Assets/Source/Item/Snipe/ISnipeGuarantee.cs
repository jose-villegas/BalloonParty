namespace BalloonParty.Item.Snipe
{
    /// <summary>Makes the next ball the thrower loads a Snipe lance, whatever the Sharpshooter roll would say.</summary>
    internal interface ISnipeGuarantee
    {
        /// <summary>The next ball loaded is armed with no roll and no cooldown, once.</summary>
        void GuaranteeNext();
    }
}
