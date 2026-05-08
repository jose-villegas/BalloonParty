using UniRx;
using UnityEngine;
using BalloonParty.Balloon.View;

namespace BalloonParty.Balloon.Model
{
    public class BalloonModel
    {
        public ReactiveProperty<string> Color { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new ReactiveProperty<Vector2Int>();
        public ReactiveProperty<bool> IsStable { get; } = new ReactiveProperty<bool>(true);

        // Weight influences which empty slot a balloon moves to during balancing.
        public int SlotWeight { get; set; }

        // Set by BalloonController when the view is bound, so other systems can reach the transform.
        public BalloonView View { get; set; }
    }
}