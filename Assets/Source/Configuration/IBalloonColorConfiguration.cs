using UnityEngine;

namespace BalloonParty.Configuration
{
    public interface IBalloonColorConfiguration
    {
        string Name { get; }
        Color Color { get; }
    }
}
