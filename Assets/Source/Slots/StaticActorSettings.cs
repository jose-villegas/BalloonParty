namespace BalloonParty.Slots
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

