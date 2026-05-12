using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Shared.Pool
{
    public class PoolManager
    {
        private readonly Dictionary<string, IPoolChannel> _channels = new();
        private Transform _root;

        private Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[Pool]");
                    Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }

                return _root;
            }
        }

        /// <summary>
        ///     Throws if the key is already taken. Call once during setup.
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

        public void Return<TItem>(string key, TItem item)
            where TItem : Component, IPoolable
        {
            GetChannel<TItem>(key).Return(item);
        }

        /// <summary>
        ///     Register a channel if the key is not already taken, then return the item.
        ///     Use this for prefab-keyed channels (e.g. VFX) where many channels share the same
        ///     type but differ by asset — the key disambiguates.
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
        ///     Access the underlying channel if needed (e.g. for direct channel reference in auto-return callbacks).
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
    }
}
