using System;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Configuration.Buffs
{
    [Serializable]
    internal class BuffEntry
    {
        [SerializeField] private ProjectileBuffId _id;
        [SerializeField] private float _value = 1f;

        public ProjectileBuffId Id => _id;
        public float Value => _value;
    }
}
