using UniRx;
using UnityEngine;
using BalloonParty.Configuration;

namespace BalloonParty.Balloon.Model
{
    public class BalloonModel
    {
        public ReactiveProperty<string> Color { get; } = new();
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);

        // Weight influences which empty slot a balloon moves to during balancing.
        public int SlotWeight { get; set; }
    }
}