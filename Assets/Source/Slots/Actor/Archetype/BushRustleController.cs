using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Spawns rustle VFX when the projectile passes near a bush slot, or when a reported
    /// impact lands within a slot's radius. Driven each frame by <see cref="BushView"/>
    /// and operates on the slot world positions handed to it after each rebuild.
    /// </summary>
    internal sealed class BushRustleController
    {
        private readonly ProjectilePositionProvider _projectileProvider;
        private readonly ImpactEventBus _impactBus;
        private readonly PoolManager _poolManager;
        private readonly IBushSettings _settings;
        private readonly HashSet<int> _rustledSlots = new();

        private IReadOnlyList<Vector2> _slotPositions = System.Array.Empty<Vector2>();

        internal BushRustleController(
            ProjectilePositionProvider projectileProvider,
            ImpactEventBus impactBus,
            PoolManager poolManager,
            IBushSettings settings)
        {
            _projectileProvider = projectileProvider;
            _impactBus = impactBus;
            _poolManager = poolManager;
            _settings = settings;
        }

        internal void SetSlots(IReadOnlyList<Vector2> slotPositions)
        {
            _slotPositions = slotPositions;
            _rustledSlots.Clear();
        }

        internal void Tick()
        {
            if (_projectileProvider == null || _settings == null)
            {
                return;
            }

            var vfxPrefab = _settings.BushRustleVfx;
            if (vfxPrefab == null)
            {
                return;
            }

            ProcessImpacts(vfxPrefab);

            if (!_projectileProvider.IsActive)
            {
                if (_rustledSlots.Count > 0)
                {
                    _rustledSlots.Clear();
                }

                return;
            }

            var projectilePos = _projectileProvider.Position;

            for (var i = 0; i < _slotPositions.Count; i++)
            {
                if (_rustledSlots.Contains(i))
                {
                    continue;
                }

                var slotPos = _slotPositions[i];

                if (!MathUtils.WithinRadius(projectilePos, slotPos, _settings.RustleProximityRadius))
                {
                    continue;
                }

                _rustledSlots.Add(i);
                SpawnRustleVfx(vfxPrefab, slotPos);
            }
        }

        private void ProcessImpacts(ParticleSystem vfxPrefab)
        {
            var impacts = _impactBus.Pending;
            if (impacts.Count == 0)
            {
                return;
            }

            for (var j = 0; j < impacts.Count; j++)
            {
                var impact = impacts[j];

                for (var i = 0; i < _slotPositions.Count; i++)
                {
                    var slotPos = _slotPositions[i];

                    if (MathUtils.WithinRadius(impact.Position, slotPos, impact.Radius))
                    {
                        SpawnRustleVfx(vfxPrefab, slotPos);
                    }
                }
            }
        }

        private void SpawnRustleVfx(ParticleSystem vfxPrefab, Vector2 slotPos)
        {
            _poolManager.PlayParticle(vfxPrefab, new Vector3(slotPos.x, slotPos.y, 0f));
        }
    }
}
