namespace BalloonParty.Slots.Actor.Archetype
{
    // Assets store the number, so a value keeps its number for good: append, never reorder.
    public enum GridActorType
    {
        Puff = 0,
        Bush = 1,
        Deflector = 2,
        Absorber = 3,
        Gatekeeper = 4,

        // A permanent bumper of the meadow (@ref plan_meadow_mushrooms). Not a catalogue piece: the level
        // generator gates it by the level's place in its meadow stretch.
        Mushroom = 5
    }
}
