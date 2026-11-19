using System;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>How a <see cref="BalloonWander" /> drifts, in canvas units and seconds.</summary>
    [Serializable]
    internal sealed class BalloonWanderSettings
    {
        [Tooltip("Its cruising speed.")]
        [SerializeField] [Min(0f)] private float _speed = 16f;

        [Tooltip("How quickly it changes speed or direction.")]
        [SerializeField] [Min(0f)] private float _acceleration = 12f;

        [Tooltip("It heads somewhere new on reaching where it was going within this, or after Retarget Seconds.")]
        [SerializeField] [Min(0.1f)] private float _arrivedWithin = 6f;

        [SerializeField] [Min(0.5f)] private float _retargetSeconds = 7f;

        [Tooltip("Its bob up and down, in canvas units; keep it within the gap kept between the balloons.")]
        [SerializeField] [Min(0f)] private float _bob = 1.5f;

        [Tooltip("Bobs a second.")]
        [SerializeField] [Min(0f)] private float _bobSpeed = 0.6f;

        internal float Speed => _speed;
        internal float Acceleration => _acceleration;
        internal float ArrivedWithin => _arrivedWithin;
        internal float RetargetSeconds => _retargetSeconds;
        internal float Bob => _bob;
        internal float BobSpeed => _bobSpeed;
    }
}
