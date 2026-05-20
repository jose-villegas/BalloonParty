namespace BalloonParty.Slots.StaticActor
{
    internal class StaticActorSettings
    {
        internal StaticActorView Prefab { get; }

        internal StaticActorSettings(StaticActorView prefab)
        {
            Prefab = prefab;
        }
    }
}

