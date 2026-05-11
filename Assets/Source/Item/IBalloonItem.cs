#region

using BalloonParty.Balloon.Model;
using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        void Setup(IBalloonModel balloon, Vector3 worldPosition);
    }
}
