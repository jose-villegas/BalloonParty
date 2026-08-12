namespace BalloonParty.Item
{
    /// <summary>
    ///     A board view that may be hosting a spinning item visual, exposing it read-only.
    /// </summary>
    /// <remarks>
    ///     Lets a consumer that only holds an <c>ISlotActorView</c> — the aim-time range telegraph
    ///     (<c>Preview/ItemRangePreviewController</c>) — read a Laser's current angle without naming
    ///     <c>BalloonView</c>, keeping the dependency pointing Balloon → Item the way the rest of this
    ///     folder does. <c>ShotBoardGather</c> reaches the same component by casting the concrete view,
    ///     which it can afford to do from the solver.
    ///     <para>
    ///         Deliberately hands back <see cref="ISpinningItemVisual" /> and not
    ///         <see cref="ITransformCapture" />: the latter's <c>CaptureSnapshot</c> is destructive (it
    ///         stops the spin — see <c>LaserItemRotation</c>), and a telegraph must never perturb the
    ///         thing it is telegraphing.
    ///     </para>
    /// </remarks>
    internal interface IHostsSpinningItem
    {
        /// <summary>The hosted item's spin state, or null when it hosts no item or a non-spinning one.</summary>
        ISpinningItemVisual SpinningItem { get; }
    }
}
