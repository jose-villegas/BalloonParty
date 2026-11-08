namespace BalloonParty.Scenario
{
    /// <summary>The sky-scatter backdrop, as an opaque backdrop drawn over it (the ground) sees it.</summary>
    internal interface ISkyBackdrop
    {
        /// <summary>Hides the sky while something opaque covers it, so its per-pixel ring loop isn't paid for
        /// under pixels nobody sees.</summary>
        void SetVisible(bool visible);
    }
}
