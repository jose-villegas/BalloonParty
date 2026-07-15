using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Configuration.Buffs
{
    [CreateAssetMenu(menuName = "Configuration/Buff Configuration", fileName = "BuffConfiguration")]
    public class BuffConfiguration : ScriptableObject, IBuffConfiguration
    {
        [SerializeField] private List<BuffEntry> _entries;

        public float GetValue(ProjectileBuffId id)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return _entries[i].Value;
                }
            }

            throw new InvalidOperationException($"No BuffEntry for buff id '{id}'.");
        }
    }
}
