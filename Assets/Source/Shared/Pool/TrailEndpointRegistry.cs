using System.Collections.Generic;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Maps a key to the <see cref="ITrailEndpoint" /> registered under it. Endpoint views register
    ///     themselves once (score bars at <c>Start</c>, fixed anchors at scope build); trail controllers
    ///     resolve by key when they spawn. Registrations are long-lived (the endpoints are persistent
    ///     views), so this is not reset per run.
    /// </summary>
    internal sealed class TrailEndpointRegistry
    {
        private readonly Dictionary<string, ITrailEndpoint> _endpoints = new();

        public void Register(string key, ITrailEndpoint endpoint)
        {
            _endpoints[key] = endpoint;
        }

        public bool TryGet(string key, out ITrailEndpoint endpoint)
        {
            return _endpoints.TryGetValue(key, out endpoint);
        }
    }
}
