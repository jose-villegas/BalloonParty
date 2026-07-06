using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>Abstract base for poolable effect MonoBehaviours; pool via <see cref="SimplePoolChannel{TItem}" />.</summary>
    public abstract class EffectView : MonoBehaviour, IPoolable, IEffect
    {
        protected Action OnComplete;

        private PoolManager _pool;
        private string _poolKey;
        private Action _selfReturn;

        internal Action ReturnToPool => _selfReturn;

        public virtual void OnSpawned() { }

        public virtual void OnDespawned()
        {
            OnComplete = null;
        }

        // Created once per instance and survives despawns to avoid a per-call allocation.
        internal void BindPool(PoolManager pool, string key)
        {
            _pool = pool;
            _poolKey = key;
            _selfReturn ??= () => _pool.Return(_poolKey, this);
        }

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

        protected void InvokeComplete()
        {
            var callback = OnComplete;
            OnComplete = null;
            callback?.Invoke();
        }
    }
}
