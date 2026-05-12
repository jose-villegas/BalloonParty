using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Abstracts any visual effect that can be triggered at a world position and
    ///     optionally tinted. Not tied to any particular rendering technology
    ///     (particle systems, animators, line renderers, etc.) and not coupled to
    ///     the pooling or item systems — safe to use from any other system.
    /// </summary>
    public interface IEffect
    {
        void Play(Vector3 position, Color tint, Action onComplete = null);
        void Play(Vector3 position, Quaternion rotation, Color tint, Action onComplete = null);
        void Stop();
    }
}

