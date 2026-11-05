using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile
{
    internal sealed class ProjectileRegistry : IProjectileRegistryWriter, IDisposable
    {
        // A handful of shots at most, so linear scans beat any keyed structure and allocate nothing.
        private readonly List<Entry> _inFlight = new();
        private readonly List<IProjectileModel> _tracked = new();
        private readonly Subject<IProjectileModel> _retired = new();

        private IProjectileModel _loaded;
        private Transform _loadedTransform;

        public IProjectileModel Loaded => _loaded;
        public int InFlightCount => _inFlight.Count;
        public IObservable<IProjectileModel> Retired => _retired;

        public void Dispose()
        {
            _retired.Dispose();
        }

        public IProjectileModel InFlightAt(int index)
        {
            return _inFlight[index].Projectile;
        }

        public Vector3 InFlightPositionAt(int index)
        {
            var transform = _inFlight[index].Transform;
            return transform != null ? transform.position : Vector3.zero;
        }

        public bool IsInFlight(IProjectileModel projectile)
        {
            return IndexOfInFlight(projectile) >= 0;
        }

        public bool IsAlive(IProjectileModel projectile)
        {
            return projectile != null && (projectile == _loaded || IndexOfInFlight(projectile) >= 0);
        }

        public bool IsTracked(IProjectileModel projectile)
        {
            return projectile != null && _tracked.Contains(projectile);
        }

        public IProjectileModel ShotOrCurrent(IProjectileModel shot)
        {
            if (shot != null)
            {
                return IsAlive(shot) ? shot : null;
            }

            return _inFlight.Count > 0 ? _inFlight[_inFlight.Count - 1].Projectile : _loaded;
        }

        public bool TryGetLatestInFlight(out IProjectileModel projectile, out Vector3 position)
        {
            var last = _inFlight.Count - 1;
            if (last < 0)
            {
                projectile = null;
                position = Vector3.zero;
                return false;
            }

            projectile = _inFlight[last].Projectile;
            position = InFlightPositionAt(last);
            return true;
        }

        public void Load(IProjectileModel projectile, Transform transform)
        {
            _loaded = projectile;
            _loadedTransform = transform;
            Track(projectile);
        }

        public void Launch(IProjectileModel projectile)
        {
            if (projectile == null || projectile != _loaded)
            {
                return;
            }

            _inFlight.Add(new Entry(_loaded, _loadedTransform));
            _loaded = null;
            _loadedTransform = null;
        }

        public void Unlaunch(IProjectileModel projectile)
        {
            var index = IndexOfInFlight(projectile);
            if (index < 0)
            {
                return;
            }

            _loaded = projectile;
            _loadedTransform = _inFlight[index].Transform;
            _inFlight.RemoveAt(index);
        }

        public void Land(IProjectileModel projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (projectile == _loaded)
            {
                _loaded = null;
                _loadedTransform = null;
            }

            var index = IndexOfInFlight(projectile);
            if (index >= 0)
            {
                _inFlight.RemoveAt(index);
            }
        }

        public void Retire(IProjectileModel projectile)
        {
            Land(projectile);
            if (projectile != null && _tracked.Remove(projectile))
            {
                _retired.OnNext(projectile);
            }
        }

        public void Clear()
        {
            _inFlight.Clear();
            _loaded = null;
            _loadedTransform = null;

            // Back to front: a Retired subscriber may itself read the registry.
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                if (i >= _tracked.Count)
                {
                    continue;
                }

                var projectile = _tracked[i];
                _tracked.RemoveAt(i);
                _retired.OnNext(projectile);
            }
        }

        private void Track(IProjectileModel projectile)
        {
            if (projectile != null && !_tracked.Contains(projectile))
            {
                _tracked.Add(projectile);
            }
        }

        private int IndexOfInFlight(IProjectileModel projectile)
        {
            if (projectile == null)
            {
                return -1;
            }

            for (var i = 0; i < _inFlight.Count; i++)
            {
                if (_inFlight[i].Projectile == projectile)
                {
                    return i;
                }
            }

            return -1;
        }

        private readonly struct Entry
        {
            internal readonly IProjectileModel Projectile;
            internal readonly Transform Transform;

            internal Entry(IProjectileModel projectile, Transform transform)
            {
                Projectile = projectile;
                Transform = transform;
            }
        }
    }
}
