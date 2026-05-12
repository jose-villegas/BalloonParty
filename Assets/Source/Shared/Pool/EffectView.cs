using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Abstract base for poolable effect MonoBehaviours. Subclass with
    ///     <see cref="ParticleEffectView" /> (ParticleSystem) or
    ///     <see cref="AnimatorEffectView" /> (Animator). Pool via
    ///     <see cref="EffectPoolChannel" />.
    /// </summary>
    public abstract class EffectView : MonoBehaviour, IPoolable, IEffect
    {
        protected Action OnComplete;

        // ── IPoolable ─────────────────────────────────────────────────────────────

        public virtual void OnSpawned() { }

        public virtual void OnDespawned()
        {
            OnComplete = null;
        }

        // ── IEffect ───────────────────────────────────────────────────────────────

        public abstract void Play(Vector3 position, Color tint, Action onComplete = null);

        public void Play(Vector3 position, Quaternion rotation, Color tint, Action onComplete = null)
        {
            transform.rotation = rotation;
            Play(position, tint, onComplete);
        }

        public void Stop()
        {
            OnDespawned();
        }

        // ── Protected helpers ─────────────────────────────────────────────────────

        protected void InvokeComplete()
        {
            var callback = OnComplete;
            OnComplete = null;
            callback?.Invoke();
        }
    }
}
