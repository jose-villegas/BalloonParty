using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;

namespace BalloonParty.Configuration.GridActors
{
    public interface IGridActorConfiguration
    {
        IReadOnlyList<GridActorPrefabEntry> Entries { get; }
    }
}
