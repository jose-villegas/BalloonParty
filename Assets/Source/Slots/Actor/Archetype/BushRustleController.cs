using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>Spawns rustle VFX when any shot in flight passes near a bush slot, or an impact lands within one.</summary>
    internal sealed class BushRustleController
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly ImpactEventBus _impactBus;
        private readonly PoolManager _poolManager;
        private readonly IBushSettings _settings;
        private readonly HashSet<int> _rustledSlots = new();

        private IReadOnlyList<Vector2> _slotPositions = System.Array.Empty<Vector2>();

        internal BushRustleController(
            IProjectileRegistry projectiles,
            ImpactEventBus impactBus,
            PoolManager poolManager,
            IBushSettings settings)
        {
            _projectiles = projectiles;
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
            if (_projectiles == null || _settings == null)
            {
                return;
            }

            var vfxPrefab = _settings.BushRustleVfx;
            if (vfxPrefab == null)
            {
                return;
            }

            ProcessImpacts(vfxPrefab);

            if (_projectiles.InFlightCount == 0)
            {
                if (_rustledSlots.Count > 0)
                {
                    _rustledSlots.Clear();
                }

                return;
            }

            UpdateRustleProximity(vfxPrefab);
        }

        private void UpdateRustleProximity(ParticleSystem vfxPrefab)
        {
            for (var i = 0; i < _slotPositions.Count; i++)
            {
                var slotPos = _slotPositions[i];

                if (!AnyShotWithin(slotPos))
                {
                    // Re-arm so a later pass (e.g. after a bounce) rustles again.
                    _rustledSlots.Remove(i);
                    continue;
                }

                // Add is true only on the rising edge into the radius.
                if (_rustledSlots.Add(i))
                {
                    SpawnRustleVfx(vfxPrefab, slotPos);
                }
            }
        }

        private bool AnyShotWithin(Vector2 slotPos)
        {
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                if (_projectiles.InFlightPositionAt(i).WithinRadius(slotPos, _settings.RustleProximityRadius))
                {
                    return true;
                }
            }

            return false;
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

                    if (impact.Position.WithinRadius(slotPos, impact.Radius))
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
