using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Shared.Pool
{
    internal class PoolManager
    {
        private readonly Dictionary<string, IPoolChannel> _channels = new();
        private readonly Dictionary<Object, string> _prefabKeys = new();
        private Transform _root;

        private Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[Pool]");
                    // DontDestroyOnLoad throws outside play mode.
                    if (Application.isPlaying)
                    {
                        Object.DontDestroyOnLoad(go);
                    }

                    _root = go.transform;
                }

                return _root;
            }
        }

        /// <summary>
        ///     Throws if the key is already taken.
        /// </summary>
        public void Register<TItem>(string key, PoolChannel<TItem> channel)
            where TItem : Component, IPoolable
        {
            if (!_channels.TryAdd(key, channel))
            {
                throw new InvalidOperationException(
                    $"Pool channel '{key}' is already registered.");
            }

            var container = new GameObject(key);
            container.transform.SetParent(Root);
            channel.SetParent(container.transform);
        }

        /// <summary>
        ///     Throws if the key is already taken. Homes the channel under <paramref name="container"/>
        ///     instead of creating one under <c>[Pool]</c> — for pools whose items should live where
        ///     they're consumed (e.g. UI notices under their bar) rather than reparenting per lifecycle.
        /// </summary>
        public void Register<TItem>(string key, PoolChannel<TItem> channel, Transform container)
            where TItem : Component, IPoolable
        {
            if (!_channels.TryAdd(key, channel))
            {
                throw new InvalidOperationException(
                    $"Pool channel '{key}' is already registered.");
            }

            channel.SetParent(container);
        }

        public void Register<TItem>(PoolChannel<TItem> channel)
            where TItem : Component, IPoolable
        {
            Register(channel.GetType().Name, channel);
        }

        public TItem Get<TItem>(string key)
            where TItem : Component, IPoolable
        {
            return GetChannel<TItem>(key).Get();
        }

        public bool IsRegistered(string key)
        {
            return _channels.ContainsKey(key);
        }

        /// <summary>
        ///     Cached pool key for a prefab — <c>Object.name</c> allocates on every access.
        /// </summary>
        public string KeyFor(Object prefab)
        {
            if (!_prefabKeys.TryGetValue(prefab, out var key))
            {
                key = prefab.name;
                _prefabKeys[prefab] = key;
            }

            return key;
        }

        public void Return<TItem>(string key, TItem item)
            where TItem : Component, IPoolable
        {
            GetChannel<TItem>(key).Return(item);
        }

        /// <summary>
        ///     Registers a channel if the key is not already taken, then returns the item.
        /// </summary>
        public TItem GetOrRegister<TItem>(string key, Func<PoolChannel<TItem>> factory)
            where TItem : Component, IPoolable
        {
            if (!_channels.ContainsKey(key))
            {
                Register(key, factory());
            }

            return Get<TItem>(key);
        }

        /// <summary>
        ///     Access the underlying channel directly (e.g. for auto-return callbacks).
        /// </summary>
        public PoolChannel<TItem> GetChannel<TItem>(string key)
            where TItem : Component, IPoolable
        {
            if (_channels.TryGetValue(key, out var channel))
            {
                return (PoolChannel<TItem>)channel;
            }

            throw new InvalidOperationException(
                $"Pool channel '{key}' not registered. Call Register() first.");
        }

        public bool TryGetChannel<TItem>(string key, out PoolChannel<TItem> channel)
            where TItem : Component, IPoolable
        {
            if (_channels.TryGetValue(key, out var raw))
            {
                channel = (PoolChannel<TItem>)raw;
                return true;
            }

            channel = null;
            return false;
        }

        /// <summary>
        ///     Synchronously pre-warms a registered channel — use only for lightweight items.
        /// </summary>
        public void Prewarm(string key, int count)
        {
            if (!_channels.TryGetValue(key, out var channel))
            {
                throw new InvalidOperationException(
                    $"Pool channel '{key}' not registered. Call Register() first.");
            }

            channel.Prewarm(count);
        }

        /// <summary>
        ///     Pre-warms a registered channel, one item per frame.
        /// </summary>
        public UniTask PrewarmAsync(string key, int count, CancellationToken ct = default)
        {
            if (!_channels.TryGetValue(key, out var channel))
            {
                throw new InvalidOperationException(
                    $"Pool channel '{key}' not registered. Call Register() first.");
            }

            return channel.PrewarmAsync(count, ct);
        }

        /// <summary>
        ///     Tops up every registered channel to the count <paramref name="counts"/> specifies.
        /// </summary>
        public async UniTask PrewarmAllAsync(
            IReadOnlyDictionary<string, int> counts,
            CancellationToken ct = default)
        {
            foreach (var (key, count) in counts)
            {
                if (_channels.TryGetValue(key, out var channel))
                {
                    var needed = count - channel.AvailableCount;
                    if (needed > 0)
                    {
                        await channel.PrewarmAsync(needed, ct);
                    }
                }
            }
        }
    }
}
