using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        void Setup(IBalloonModel balloon, Vector3 worldPosition);
    }
}
