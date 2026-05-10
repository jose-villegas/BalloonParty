using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Balloon.Items
{
    public interface IItemView
    {
        ItemType Type { get; }
        void Activate(Color balloonColor);
        void Deactivate();
        void ApplySortingOrder(int startOrder);
    }
}

