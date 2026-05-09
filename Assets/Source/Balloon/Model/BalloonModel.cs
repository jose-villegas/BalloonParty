using BalloonParty.Balloon.View;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    public class BalloonModel
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);

        // Weight influences which empty slot a balloon moves to during balancing.
        public int SlotWeight { get; set; }

        // Set by BalloonController when the view is bound, so other systems can reach the transform.
        public BalloonView View { get; set; }
    }
}