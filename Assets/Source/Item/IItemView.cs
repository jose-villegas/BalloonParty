using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Item
{
    public interface IItemView
    {
        void Activate(Color balloonColor);
        void Deactivate();
        void ApplySortingOrder(int startOrder);
    }
}
