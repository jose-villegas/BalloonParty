using System;
using UnityEngine;
using BalloonParty.Shared.Pool;

namespace BalloonParty.Shared
{
    /// <summary>A tintable, positionable visual effect, agnostic to the underlying rendering technology.</summary>
    public interface IEffect
    {
        void Play(Vector3 position, Color tint, Action onComplete = null);
        void Play(Vector3 position, Quaternion rotation, Color tint, Action onComplete = null);
        void Stop();
    }
}
