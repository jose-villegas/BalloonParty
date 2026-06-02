using System.Collections.Generic;

namespace BalloonParty.Configuration
{
    public interface IGridActorConfiguration
    {
        IReadOnlyList<GridActorPrefabEntry> Entries { get; }
    }
}

