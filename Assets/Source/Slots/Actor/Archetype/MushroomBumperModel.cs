using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     A meadow mushroom (@ref plan_meadow_mushrooms): a permanent bumper the ball bounces off. It stands in
    ///     its slot for the whole level, as a bush does: it is never hit, never popped, and nothing moves it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It has no collider and takes no part in the hit pipeline: the ball's bounce is worked out against
    ///         the circles of <c>MushroomBoard</c>, which reads these models off the grid.
    ///     </para>
    ///     <para>
    ///         What keeps it still is what it is not. The balance race, the wave-start roam and pressure shoves
    ///         only ever move an <c>IWriteableDynamicSlotActor</c>, which this is not; a snake walks only through
    ///         balloons; and it is not <c>IPassThrough</c>, so a rising balloon stops under it, as under a bush.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomBumperModel : IGridActorModel
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        /// <summary>
        ///     Which cap of the level this is, 0 or more: its place in the layout the level was dealt. It becomes
        ///     the id of the cap's bumper circle (<c>BumperCircle.Id</c>), which a bounce names its cap by.
        /// </summary>
        public int ChainIndex { get; }

        /// <summary>
        ///     Which wall the cap belongs to, 0 or more (<c>BumperCircle.Wall</c>): the caps of one wall stand in
        ///     neighbouring slots, count as one hit and never kick the ball to each other.
        /// </summary>
        public int Wall { get; }

        public GridActorType ActorType => GridActorType.Mushroom;
        public SlotActorKind Kind => SlotActorKind.Static;

        /// <summary>A cap that is a wall by itself.</summary>
        internal MushroomBumperModel(int chainIndex)
            : this(chainIndex, chainIndex)
        {
        }

        internal MushroomBumperModel(int chainIndex, int wall)
        {
            ChainIndex = chainIndex;
            Wall = wall;
        }
    }
}
