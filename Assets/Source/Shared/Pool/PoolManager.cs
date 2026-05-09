using System;
using System.Collections.Generic;

namespace BalloonParty.Shared
{
    public class PoolManager
    {
        private readonly Dictionary<(Type, object), object> _channels = new();

        public T Channel<T>(object key, Func<T> factory) where T : class
        {
            var compositeKey = (typeof(T), key);
            if (_channels.TryGetValue(compositeKey, out var existing))
                return (T)existing;

            var channel = factory();
            _channels[compositeKey] = channel;
            return channel;
        }

        public T Channel<T>(Func<T> factory) where T : class
        {
            return Channel(typeof(T), factory);
        }

        public T Channel<T>() where T : class
        {
            var compositeKey = (typeof(T), (object)typeof(T));
            if (_channels.TryGetValue(compositeKey, out var existing))
                return (T)existing;

            throw new InvalidOperationException(
                $"Pool channel {typeof(T).Name} not registered. Use Channel<T>(factory) first.");
        }
    }
}
