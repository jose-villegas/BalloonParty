using UnityEngine;

namespace BalloonParty.Projectile
{
    /// <summary>
    /// Lightweight shared reference to the active projectile transform.
    /// Set by <c>ThrowerController</c> on load/reload, read by systems
    /// that need the projectile's world position without direct coupling.
    /// </summary>
    internal class ProjectilePositionProvider
    {
        private Transform _transform;
        private bool _isFree;

        internal bool IsActive => _transform != null && _isFree;
        internal Vector3 Position => _transform != null ? _transform.position : Vector3.zero;

        internal void Set(Transform projectileTransform)
        {
            _transform = projectileTransform;
            _isFree = false;
        }

        internal void SetFree(bool isFree)
        {
            _isFree = isFree;
        }

        internal void Clear()
        {
            _transform = null;
            _isFree = false;
        }
    }
}
